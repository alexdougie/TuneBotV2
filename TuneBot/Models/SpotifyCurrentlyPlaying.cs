using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuneBot.Models
{
    public class SpotifyCurrentlyPlaying
    {
        public Item item { get; set; }
    }
    public class Item
    {
        public string name { get; set; }
        public List<Artist> Artists { get; set; }
    }
    public class Artist
    {
        public string name { get; set; }
    }
}
