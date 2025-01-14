using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TetraClashServer
{
    class Match
    {
        public Player Player1 { get; }
        public Player Player2 { get; }
        public bool IsActive { get; private set; }

        public Match(Player player1, Player player2)
        {
            Player1 = player1;
            Player2 = player2;
            IsActive = true;
        }

        public void EndMatch()
        {
            IsActive = false;
        }
    }
}
