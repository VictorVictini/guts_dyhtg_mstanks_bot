﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Simple
{
    /// <summary>
    /// Simple bot which performs some movements to demo API, then rotates the turret, turns the tank
    /// and moves towards the center circle at 0,0.
    /// </summary>
    public class SimpleBot
    {
        private const string ipAddress = Constant.IP;
        private const int port = Constant.Port;
        private string tankName;
        private GameObjectState ourMostRecentState;
        private GameObjectState enemyMostRecentState;
        private GameObjectState powerupMostRecentState;
        private int cntDwn; //how many times it may use an old coordinate before making it null
        private bool goToGoal = false;

        //Our TCP client.
        private TcpClient client;

        //Thread used to listen to the TCP connection, so main thread doesn't block
        private Thread listeningThread;

        //store incoming messages on the listening thread,
        //before transfering them safely onto main thread.
        private Queue<byte[]> incomingMessages;

        public bool BotQuit { get; internal set; }

        public SimpleBot(string name)
        {
            tankName = Constant.TeamName + ":" + name;
            incomingMessages = new Queue<byte[]>();

            ConnectToTcpServer();

            //wait for a bit to allow connection to establish before proceeding.
            Thread.Sleep(5000);


            SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.test));


            //send the create tank request.
            SendMessage(MessageFactory.CreateTankMessage(tankName));

            //conduct basic movement requests.
            DoAPITest();


        }


        private void DoAPITest()
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
            {
                read += chunk;
            }
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

                if (messageType == NetworkMessageType.test)
                {
                    Console.WriteLine("TEST ACK RECEIVED");
                }

                if (messageType == NetworkMessageType.objectUpdate)
                {
                    GameObjectState objectState = JsonConvert.DeserializeObject<GameObjectState>(jsonPayload);
                    //Console.WriteLine("ID: " + objectState.Id + " Type: " + objectState.Type + " Name: " + objectState.Name + " ---- " + objectState.X + "," + objectState.Y + " : " + objectState.Heading + " : " + objectState.TurretHeading);

                    if (objectState.Name == tankName)
                        ourMostRecentState = objectState;
                    else if(objectState.Type == "Tank")
                    {
                        enemyMostRecentState = objectState;
                        cntDwn = 100;
                    }
                    else if (objectState.Type == "HealthPickup" || objectState.Type == "AmmoPickup")
                    {
                        powerupMostRecentState = objectState;
                    }
                }

                else
                {
                    // snitchPickup
                    // {"Id":-2896}
                    Console.WriteLine("{" + messageType.ToString() + "}");
                    Console.WriteLine(jsonPayload);
                    if(messageType.ToString() == "kill")
                        goToGoal = true;
                    else if (messageType.ToString() == "enteredGoal")
                        goToGoal = false;
                    else if (messageType.ToString() == "destroyed")
                    {
                        goToGoal = false;
                        powerupMostRecentState = null;
                        ourMostRecentState = null;
                        enemyMostRecentState = null;
                    }
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
            {
                return;
            }
            try
            {
                // Get a stream object for writing. 			
                NetworkStream stream = client.GetStream();
                if (stream.CanWrite)
                {
                    stream.Write(message, 0, message.Length);

                }
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
            
            if(goToGoal)
            {
                //Console.WriteLine("GO TO GOAL ASSHOLE");
                float goal1Dist = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, 0, 100);
                float goal2Dist = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, 0, -100);

                if (goal1Dist <= goal2Dist)
                {
                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));
                    float targetGoal = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, 0, 100);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetGoal));
                    float goalPos = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, 0, 100);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, goalPos));
                }
                else
                {
                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));
                    float targetGoal = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, 0, -100);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetGoal));
                    float goalPos = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, 0, -100);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, goalPos));
                }

                if(ourMostRecentState.Y >= 100)
                    goToGoal = false;
                return;
            }
            // wait until we get our first state update from the server
            if (ourMostRecentState != null)
            {
                // TODO AI SHID HERE
                if(enemyMostRecentState == null)
                {
                    //Console.WriteLine("Searching for enemies");
                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.toggleTurretLeft));

                    if (powerupMostRecentState != null)
                    {
                        Console.WriteLine("PICK UP POWERUP");
                        //now let's turn the whole vehicle towards a different random point.
                        //now let's turn the whole vehicle towards a different random point.

                        //now let's turn the whole vehicle towards a different random point.
                        float targetHeading2 = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, powerupMostRecentState.X, powerupMostRecentState.Y);
                        SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetHeading2));
                        Thread.Sleep(20);

                        //now let's move to that point.
                        float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, powerupMostRecentState.X, powerupMostRecentState.Y);
                        SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, distance));
                    }
                    return;
                }
                else
                {
                    if (enemyMostRecentState.Name.Split(":")[0] == Constant.TeamName)
                    {
                        enemyMostRecentState = null;
                        return;
                    }
                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.stopTurret));
                }

                if (powerupMostRecentState != null)
                {
                    
                        Console.WriteLine("PICK UP POWERUP");
                    //let's turn the tanks turret towards a random point.
                    float targetHeading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, enemyMostRecentState.X, enemyMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnTurretToHeading, targetHeading));


                    Thread.Sleep(20);

                    //now let's turn the whole vehicle towards a different random point.
                    //now let's turn the whole vehicle towards a different random point.

                    //now let's turn the whole vehicle towards a different random point.
                    float targetHeading2 = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, powerupMostRecentState.X, powerupMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetHeading2));
                    Thread.Sleep(20);

                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.fire));

                    //now let's move to that point.
                    float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, powerupMostRecentState.X, powerupMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, distance));
                }
                else
                {
                    //let's turn the tanks turret towards a random point.
                    float targetHeading = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, enemyMostRecentState.X, enemyMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnTurretToHeading, targetHeading));


                    Thread.Sleep(20);


                    //now let's turn the whole vehicle towards a different random point.
                    float targetHeading2 = GetHeading(ourMostRecentState.X, ourMostRecentState.Y, enemyMostRecentState.X, enemyMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.turnToHeading, targetHeading2));
                    Thread.Sleep(20);

                    SendMessage(MessageFactory.CreateZeroPayloadMessage(NetworkMessageType.fire));

                    //now let's move to that point.
                    float distance = CalculateDistance(ourMostRecentState.X, ourMostRecentState.Y, enemyMostRecentState.X, enemyMostRecentState.Y);
                    SendMessage(MessageFactory.CreateMovementMessage(NetworkMessageType.moveForwardDistance, distance));
                }
                
                if(cntDwn-- <= 0)
                    enemyMostRecentState = null;
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

    public static class MessageFactory
    {

        public static byte[] CreateTankMessage(string name)
        {

            string json = JsonConvert.SerializeObject(new { Name = name });
            byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(json);
            return AddTypeAndLengthToArray(clientMessageAsByteArray, (byte)NetworkMessageType.createTank);
        }

        public static byte[] CreateMovementMessage(NetworkMessageType type, float amount)
        {
            string json = JsonConvert.SerializeObject(new { Amount = amount });
            byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(json);
            return AddTypeAndLengthToArray(clientMessageAsByteArray, (byte)type);
        }

        public static byte[] AddTypeAndLengthToArray(byte[] bArray, byte type)
        {
            byte[] newArray = new byte[bArray.Length + 2];
            bArray.CopyTo(newArray, 2);
            newArray[0] = type;
            newArray[1] = (byte)bArray.Length;
            return newArray;
        }

        public static byte[] CreateZeroPayloadMessage(NetworkMessageType type)
        {

            byte[] message = new byte[2];
            message[0] = (byte)type;
            message[1] = 0;
            return message;
        }


    }

    public enum NetworkMessageType
    {
        test = 0,
        createTank = 1,
        despawnTank = 2,
        fire = 3,
        toggleForward = 4,
        toggleReverse = 5,
        toggleLeft = 6,
        toggleRight = 7,
        toggleTurretLeft = 8,
        toggleTurretRight = 9,
        turnTurretToHeading = 10,
        turnToHeading = 11,
        moveForwardDistance = 12,
        moveBackwardsDistance = 13,
        stopAll = 14,
        stopTurn = 15,
        stopMove = 16,
        stopTurret = 17,
        objectUpdate = 18,
        healthPickup = 19,
        ammoPickup = 20,
        snitchPickup = 21,
        destroyed = 22,
        enteredGoal = 23,
        kill = 24,
        snitchAppeared = 25,
        gameTimeUpdate = 26,
        hitDetected = 27,
        successfulHit =28

    }

    public class GameObjectState
    {
        public int Id;
        public string Name;
        public string Type;
        public float X;
        public float Y;
        public float Heading;
        public float TurretHeading;
        public int Health;
        public int Ammo;
    }




}
