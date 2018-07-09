// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Linq;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            //args = new[] { "test" };

            if (args == null || args.Length == 0)
            {
                Client.Start();
            }
            else
            {
                Worker.Start(args.First());
            }
        }
    }
}