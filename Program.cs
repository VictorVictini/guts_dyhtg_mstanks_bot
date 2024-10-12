using System.Threading;

namespace Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            SimpleBot bot = new SimpleBot();


            while (!bot.BotQuit)
            {

                bot.Update();

                //run at 60Hz
                //testing
                Thread.Sleep(16);

            }
        }
    }
}
