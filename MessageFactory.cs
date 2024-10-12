using Newtonsoft.Json;
using System.Text;

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