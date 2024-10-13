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
                bots[i] = new Thread(new ParameterizedThreadStart(CreateBot));
                bots[i].Start(Constant.Names[i]);
            }
            while (true) {}
        }
        private static void CreateBot(object name)
        {
            SimpleBot bot = new SimpleBot(name as string);

            while (!bot.BotQuit)
            {

                bot.Update();

                //run at 60Hz
                Thread.Sleep(16);

            }
        }
    }
}
