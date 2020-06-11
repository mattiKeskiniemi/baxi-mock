﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Xml;
using Baximocker.Interfaces;
using Microsoft.AspNetCore.Builder;
using Baximocker.Models;
using WebSocketSharp;
using WebSocketSharp.Server;


namespace Baximocker
{
    /// <summary>
    /// The basic flow of Baxi payments is as follows:
    /// 1) Open connetion with Open message -> Baxi server does not respond to this
    /// 2) Create payment to Baxi -> Baxi starts to send current situation to client -> this can be skipped, since this is just a noice
    /// 3) Payment is finished after it is paid, encounters an error or is canceled. Payment is pending until that
    /// </summary>
    public class BaxiMessagehandler: WebSocketBehavior
    {
        public List<CardPayment> ActivePayments = new List<CardPayment>();

        private static int TransActionOK = 0;
        private static int AdminTransActionOK = 1;
        private bool PaymentWillSucceed = true;
        private int PaymentId = 0;
        protected override void OnMessage(MessageEventArgs e)
        {
            var message = e.Data;
            if (message.Contains("Open"))
            {
                Send(message);
            }
            else
            {
                var xmlMessage = GetMessageXMLFromData(e.RawData);
                HandleMessage(xmlMessage);

            }

        

        }

        private void HandleMessage(XmlDocument xmlMessage)
        {
            int transactionType = 0;
            var transactionTypeNode = xmlMessage.SelectSingleNode("NetsRequest/Dfs13TransferAmount/TransactionType");
            if (transactionTypeNode != null)
            {
              var  transactionTypeString = transactionTypeNode.InnerText;
              transactionType = int.Parse(transactionTypeString);
            }

            if (transactionType == 48)
            {
                var terminalID = long.Parse( xmlMessage.SelectSingleNode("NetsRequest/MessageHeader/@TerminalID").InnerText);
                var payment = new CardPayment
                {
                    TerminalId  = terminalID,
                    PaymentId = PaymentId
                };
                ActivePayments.Add(payment);
                PaymentId++;
                Console.WriteLine("Will this payment (S)ucceed or (F)ail?");
                var response = Console.ReadKey();
                if (response.Key == ConsoleKey.S)
                {
                    SetPaymentAsSuccessful(terminalID);
                }
                else
                {
                    SetPaymentAsFailed(terminalID);
                }
            }
            
        }

      


        private XmlDocument GetMessageXMLFromData(byte[] rawData)
        {
            var xml = new XmlDocument();
            var document = Encoding.UTF8.GetString(rawData);
            try
            {
                xml.LoadXml(document);
                return xml;
            }
            catch (XmlException e)
            {
                var message = "Unable to parse XML document: " + document;
                throw;
            }
        }


        private void SetPaymentAsFailed( long terminalId)
        {
            var payment = ActivePayments.First(ap => ap.TerminalId == terminalId);
            ActivePayments.Remove(payment);
            var errorMessage = new XmlDocument();
            errorMessage.LoadXml(BaxiClientMessages.ErrorPayment);
            var terminalNode = errorMessage.SelectSingleNode("NetsResponse/MessageHeader/@TerminalID");
            var errorNode = errorMessage.SelectSingleNode("NetsResponse/Dfs13Error/ErrorString");
            errorNode.InnerText = terminalId.ToString();

            //send as Byte array
            Send(Encoding.Default.GetBytes(errorMessage.OuterXml));
        }
        private void SetPaymentAsSuccessful(long terminalId)
        {
            var payment = ActivePayments.First(ap => ap.TerminalId == terminalId);
            ActivePayments.Remove(payment);
            var successfulmMessage = new XmlDocument();
            successfulmMessage.LoadXml(BaxiClientMessages.SuccessfulPayment);
            //set node values
            var resultNode = successfulmMessage.SelectSingleNode("NetsResponse/Dfs13LocalMode/Result");

            resultNode.InnerText = TransActionOK.ToString();

            var terminalNode = successfulmMessage.SelectSingleNode("NetsResponse/Dfs13LocalMode/TerminalID");
            terminalNode.InnerText = terminalId.ToString();
            //receiving end expects raw byte array
            Send(Encoding.Default.GetBytes(successfulmMessage.OuterXml));
        }
    }

  
}