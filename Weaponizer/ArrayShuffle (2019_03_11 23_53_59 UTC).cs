using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Weaponizer
{
    class ArrayShuffle
    {
        public static int[] Shuffle (int[] array)
        {
            Random rnd = new Random();
            // create randomized indices
            int[] rndIndex = Enumerable.Range(0, array.Length).OrderBy(r => rnd.Next()).ToArray();
            // apply randomized indices to create new array
            int[] newArray = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[rndIndex[i]];
            }
            // here ya go
            return newArray;
        }

        public static byte[] Shuffle (byte[] array)
        {
            Random rnd = new Random();
            // create randomized indices
            int[] rndIndex = Enumerable.Range(0, array.Length).OrderBy(r => rnd.Next()).ToArray();
            // apply randomized indices to create new array
            byte[] newArray = new byte[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[rndIndex[i]];
            }
            // here ya go
            return newArray;
        }
    }
}
