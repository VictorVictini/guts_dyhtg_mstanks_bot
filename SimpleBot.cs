﻿using Newtonsoft.Json;
using System.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace Simple
{
    /// <summary>
    /// Simple bot which performs some movements to demo API, then rotates the turret, turns the tank
    /// and moves towards the center circle at 0,0.
    /// </summary>
    public class SimpleBot
    {
        private const string ipAddress = constant.IP;
        private const int port = constant.Port;
        private string tankName;
        private GameObjectState ourMostRecentState;

        //Our TCP client.
        private TcpClient client;

        //Thread used to listen to the TCP connection, so main thread doesn't block
        private Thread listeningThread;

        //store incoming messages on the listening thread,
        //before transfering them safely onto main thread.
        private Queue<byte[]> incomingMessages;

        public bool BotQuit { get; internal set; }

        public SimpleBot(string name = "test:SimpleBot1")
        {
            tankName = name;
            incomingMessages = new Queue<byte[]>();

            ConnectToTcpServer();
        }


        /*private void DoAPITest()
        {
            int millisecondSleepTime = 500;
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleForward));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleReverse));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopMove));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleLeft));
            Thread.Sleep(millisecondSleepTime);


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleRight));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurn));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretRight));
            Thread.Sleep(millisecondSleepTime);

            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.fire));

        }
        */


        private void ConnectToTcpServer()
        {
            try
            {
                //set up a TCP client on a background thread
                listeningThread = new Thread(new ThreadStart(ConnectAndListen));
                listeningThread.IsBackground = true;
                listeningThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("On client connect exception " + e);
            }
        }

        private void ConnectAndListen()
        {
            try
            {
                client = new TcpClient(ipAddress, port);

                //send the create tank request.
                SendMessage(MessageFactory.CreateTankMessage(tankName));

                // Get a stream object for reading 				
                using (NetworkStream stream = client.GetStream())
                {

                    while (client.Connected)
                    {
                        int type = stream.ReadByte();
                        int length = stream.ReadByte();

                        Byte[] bytes = new Byte[length];

                        //there's a JSON package
                        if (length > 0)
                        {
                            // Read incoming stream into byte arrary. 					
                            bytes = GetPayloadDataFromStream(stream, length);

                            Byte[] byteArrayCopy = new Byte[length + 2];
                            bytes.CopyTo(byteArrayCopy, 2);
                            byteArrayCopy[0] = (byte)type;
                            byteArrayCopy[1] = (byte)length;

                            lock (incomingMessages)
                            {
                                incomingMessages.Enqueue(byteArrayCopy);
                            }
                        }
                        else
                        {
                            //no JSON
                            lock (incomingMessages)
                            {
                                byte[] zeroPayloadMessage = new byte[2];
                                zeroPayloadMessage[0] = (byte)type;
                                zeroPayloadMessage[1] = 0;
                                incomingMessages.Enqueue(zeroPayloadMessage);
                            }
                        }
                    }
                }
            }
            catch (SocketException socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        private byte[] GetPayloadDataFromStream(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
                read += chunk;

            return buffer;
        }

        private void DecodeMessage(NetworkMessageType messageType, int payloadLength, byte[] bytes)
        {
            try
            {
                string jsonPayload = "";
                if (payloadLength > 0)
                {
                    var payload = new byte[payloadLength];
                    Array.Copy(bytes, 2, payload, 0, payloadLength);
                    jsonPayload = Encoding.ASCII.GetString(payload);
                    
                }

                if (messageType == NetworkMessageType.objectUpdate)
                {
                    GameObjectState objectState = JsonConvert.DeserializeObject<GameObjectState>(jsonPayload);
                    Console.WriteLine("ID: " + objectState.Id + " Type: " + objectState.Type + " Name: " + objectState.Name + " ---- " + objectState.X + "," + objectState.Y + " : " + objectState.Heading + " : " + objectState.TurretHeading);

                    if (objectState.Name == tankName)
                        ourMostRecentState = objectState;
                }

                else
                {
                    // snitchPickup
                    // {"Id":-2896}
                    Console.WriteLine(messageType.ToString());
                    Console.WriteLine(jsonPayload);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Message decode exception " + e);
            }
        }

        private void SendMessage(byte[] message)
        {
            if (client == null)
                return;

            try
            {
                // Get a stream object for writing. 			
                NetworkStream stream = client.GetStream();
                if (stream.CanWrite)
                    stream.Write(message, 0, message.Length);
            }
            catch (SocketException socketException)
            {
                Console.WriteLine("Socket exception: " + socketException);
            }
        }

        public void Update()
        {
            if (incomingMessages.Count > 0)
            {
                var nextMessage = incomingMessages.Dequeue();
                DecodeMessage((NetworkMessageType)nextMessage[0], nextMessage[1], nextMessage);
            }

            // wait until we get our first state update from the server
            if (ourMostRecentState != null)
            {
                // TODO AI SHID HERE

                /*//let's turn the tanks turret towards a random point.
                var randomTurretX = random.Next(-100, 100);
                var randomTurretY = random.Next(-100, 100);

                //let's turn the tanks turret towards a random point.
                float targetHeading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, randomTurretX, randomTurretY);
                SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnTurretToHeading, targetHeading));


                Thread.Sleep(2000);


                //now let's turn the whole vehicle towards a different random point.
                var randomX = random.Next(-100, 100);
                var randomY = random.Next(-100, 100);
                float targetHeading2 = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, randomX, randomY);
                SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetHeading2));
                Thread.Sleep(2000);



                //now let's move to that point.
                float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, randomX, randomY);
                SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, distance));

                testMovementsPerformed = true;*/
            }

        }

        private float CalculateDistance(float ownX, float ownY, float otherX, float otherY)
        {
            float headingX = otherX - ownX;
            float headingY = otherY - ownY;

            return (float)Math.Sqrt((headingX * headingX) + (headingY * headingY));
        }

        private void AimTurretToTargetHeading(float targetHeading)
        {
            float turretDiff = targetHeading - ourMostRecentState.TurretHeading;
            if (Math.Abs(turretDiff) < 5)
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));
            }
            else if (IsTurnLeft(ourMostRecentState.TurretHeading, targetHeading))
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));
            }
            else if (!IsTurnLeft(ourMostRecentState.TurretHeading, targetHeading))
            {
                SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretRight));
            }
        }

        private float GetHeading(float x1, float y1, float x2, float y2)
        {
            float heading = (float)Math.Atan2(y2 - y1, x2 - x1);
            heading = (float)RadianToDegree(heading);
            heading = (heading - 360) % 360;

            return Math.Abs(heading);
        }

        private double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        bool IsTurnLeft(float currentHeading, float desiredHeading)
        {
            float diff = desiredHeading - currentHeading;
            return diff > 0 ? diff > 180 : diff >= -180;
        }
    }
}
