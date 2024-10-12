using System.Threading;

namespace Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread[] bots = new Thread[Constant.BotsCount];
            for (int i = 0; i < bots.Length; i++)
            {
                bots[i] = new Thread(CreateBot);
                bots[i].Start();
            }
            while (true) {}
        }
        private static void CreateBot()
        {
            SimpleBot bot = new SimpleBot();

            while (!bot.BotQuit)
            {

                bot.Update();

                //run at 60Hz
                Thread.Sleep(16);

            }
        }
    }
}
