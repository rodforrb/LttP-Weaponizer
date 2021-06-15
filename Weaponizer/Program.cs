using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZCompressLibrary;

namespace Weaponizer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Enter the seed name too.");
                return;
            }
            //todo better file checking
            string fileName = args[0];
            // fix file directory stuff
            fileName = fileName.TrimStart(new Char[] { '.', '\\', '/' });

            byte[] data = File.ReadAllBytes(fileName);
            int table_address = 0x27ce0;
            int c_table_length = 799;
            Random rnd = new Random();
            string spoiler = "";

            // decompress the enemy damage table from the ROM (thanks Zarby)
            int f = 0;
            byte[] sdata = Decompress.ALTTPDecompressOverworld(data, 0x27ce0, c_table_length, ref f);
            byte[] udata = new byte[3000];
            byte[] enemy_table = new byte[5000];

            // expanding the array (unsure if needed)
            for (int i = 0; i < udata.Length; i++)
            {
                if (i < sdata.Length)
                {
                    udata[i] = sdata[i];
                }
                else
                {
                    udata[i] = 0;
                }
            }

            // split bytes
            for (int i = 0; i < 5000; i += 2)
            {
                enemy_table[i] = (byte)(udata[i / 2] >> 4);
                enemy_table[i + 1] = (byte)(udata[i / 2] & 0x0F);
            }

            // normalize the enemy damage table - default to full shuffle for now
            byte[] new_enemy_table = Enemy_Table_Setup(enemy_table, "full");

            // recombine bytes
            byte[] combined_table = new byte[2048];
            for (int i = 0; i < 2048; i += 1)
            {
                combined_table[i] = (byte)((new_enemy_table[(i * 2)] << 4) | new_enemy_table[(i * 2) + 1]);
            }
            // recompress table
            byte[] compressed_enemy_table = Compress.ALTTPCompressOverworld(combined_table, 0, combined_table.Length);

            // write table to ROM data - table cannot exceed 799 bytes length
            for (int i = 0; (i < c_table_length & i < compressed_enemy_table.Length); i++)
            {
                data[table_address + i] = compressed_enemy_table[i];
            }


            // create new damage table and write to ROM data
            byte[] new_damage_table = Create_Damage_Table();
            int damage_table_address = 0x6B8F1;
            for (int i = 0; i < 128; i++)
            {
                data[damage_table_address + i] = new_damage_table[i];
            }
            // write spoiler info
            string[] weaponClassNames = new string[] { "Boomerang", "Level 1", "Level 2", "Level 3", "Level 4", "Level 5",
                "Bow", "Hookshot", "Bombs", "Silvers", "Powder", "Fire Rod", "Ice Rod", "Bombos", "Ether", "Quake" };
            for (int i = 0; i < 16; i++)
            {
                spoiler += weaponClassNames[i] + ": " + HexToText(new_damage_table[i*8 + 1]) + "\r\n";
            }


            // randomize powder fairy prize
            int fairy_address = 0x36DD0;    // fairy, bees, appl, fish, heart, $5, $20, bomb, magic
            byte[] fairy_options = new byte[] { 0xE3, 0x79, 0xAC, 0xD2, 0xD8, 0xDA, 0xDB, 0xDC, 0xDF };
            data[fairy_address] = fairy_options[rnd.Next(fairy_options.Length)];
            spoiler += "Fairy prize: " + HexToText(data[fairy_address]) + "\r\n";


            // randomize bomb timers
            int bomb_timer_address = 0x41543;
            // write fuse timer to ROM, first byte - 40-255, needs to be 40+ to hit TT bomb attic
            data[bomb_timer_address] = (byte)(rnd.Next(215) + 0x40);
            spoiler += "Bomb timers: " + Convert.ToString(data[bomb_timer_address]) + ", ";
            // bomb explosion speed is next 10 bytes
            byte[,] rates = new byte[,] {
            { 0x02, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02, 0x02, 0x02, 0x02 }, // fastest
            { 0x03, 0x02, 0x02, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03, 0x03 }, // double speed
            { 0x06, 0x04, 0x04, 0x04, 0x04, 0x04, 0x06, 0x06, 0x06, 0x06 }, // default
            { 0x0C, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0C, 0x0C, 0x0C, 0x0C }, // half speed
            { 0x18, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x18, 0x18, 0x18, 0x18 }, // quarter speed lol
            // repeat 'moderate' ones to increase weight
            { 0x03, 0x02, 0x02, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03, 0x03 }, // double speed
            { 0x06, 0x04, 0x04, 0x04, 0x04, 0x04, 0x06, 0x06, 0x06, 0x06 }, // default
            { 0x0C, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0C, 0x0C, 0x0C, 0x0C }, // half speed
            // repeat default again for most weight
            { 0x06, 0x04, 0x04, 0x04, 0x04, 0x04, 0x06, 0x06, 0x06, 0x06 } }; // default

            // choose one of the 9 rows
            int row = rnd.Next(9);
            // write to ROM
            for (int i = 0; i<10; i++)
            {
                data[bomb_timer_address + i + 1] = rates[row, i];
                spoiler += Convert.ToString(rates[row, i]) + " ";
            }
            spoiler += "\r\n";


            // this fixes the softlock of getting stuck when hitting a frozen enemy with a freeze-effect sword
            // overwrites [ LDA $0DD0, X : CMP.b #$0B : BEQ BRANCH_THETA ] at 0x36E3A-D
            int freezeCheckAddr = 0x36E3A;
            for (int i = 0; i < 4; i++)
            {
                data[freezeCheckAddr + i] = 0xEA;
            }


            // fileouts
            // spoiler log
            File.WriteAllText("wpn_" + fileName + ".txt", spoiler);
            // ROM file
            FileStream fs = new FileStream("wpn_"+fileName, FileMode.CreateNew, FileAccess.Write);
            fs.Write(data, 0, data.Length);
            fs.Close();
        }

        /* 'normalizes' the enemy damage class table
         * @param table byte[] the enemy damage class table extracted from the ROM
         * @return byte[] the new modified table
         */
        static byte[] Enemy_Table_Setup(byte[] table, string mode = "safe")
        {
            /*  Brand new damage table rules brought to you by ya boi kapimozr
             *  class 0 = no damage (this is default)
             *  class 1 = 'regular' damage (this is what most enemies take). This will be used for as many
             *      enemies as possible, to normalize the damage/effects by weapons.
             *  class 2 = 'safe' damage (this is different). This will be used for bosses so they don't get
             *      hit with effects that delete them and cause a softlock.
             *  class 3 = not used but reserved
             *  class 4 = fire rod & bombos special category (fire rod / bombos class 4 damage is set in the
             *      damage table randomization) needed for freezors and kholdstare shell
             *  class 5 = not used
             *  class 6 = not used
             *  class 7 = not used
             */


            // these are things we don't want to set to use damage class 1, which may be volatile. unknowns are mostly environmental objects
            int[] normalDmgExceptions = new int[]
            {
                3, 4, 5, 6, 7, // switches and stuff
                9, // moldorm
                11, // chicken
                20, // idk
                21, // antifairy
                22, 26, 28, 29, 30, 31, 33, 37, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, // idk
                51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 63, // still idk
                // 64 is the electrical barrier, don't care for now
                82, // idk
                83, // armos knight
                84, // lanmolas
                87, 89, 90, // idk
                91, 92, 93, 94, 95, 96, 97, // sparks, rollers, a beamos
                98, 99, 101, 102, 103, 104, 105, 108, // idk
                112, // helmasaur fireball
                114, 115, 116, 117, 118, 120, // idk
                119, // another anti fairie or something
                122, // aghanim
                123, // aghanim ball
                125, 126, 127, 128,  // spike, firebars, firesnake
                130, // more antifairies
                135, // a fireball
                136, 137, // mothula, mothula beam
                138, // spike block
                140, // arrghus
                146, // helmasaur
                147, // idk
                149, 150, 151, 152, // eye lasers
                158, 159, 160, // idk
                162, // kholdstare
                171, 172, 173, 174, 175, 176, 177, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, // idk
                189, 190, // vitreous, eyeballs
                191, // aghanim lightning
                192, 193, // idk
                194, // boulder
                196, 197, 198, 200, // idk
                203, // trinexx
                206, // blind
                210, 213, // idk
                212, // a mine
                214, 215 // ganon
            };

            // same exceptions but for "full shuffle" 
            int[] fullDmgExceptions = new int[]
            {
                3, 4, 5, 6, 7, // switches and stuff
                9, // moldorm
                20, // idk
                22, 26, 28, 29, 30, 31, 33, 37, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, // idk
                51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 63, // still idk
                // 64 is the electrical barrier, don't care for now
                82, // idk
                83, // armos knight
                84, // lanmolas
                87, 89, 90, // idk
                98, 99, 101, 102, 103, 104, 105, 108, // idk
                114, 115, 116, 117, 118, 120, // idk
                122, // aghanim
                123, // aghanim ball
                136, // mothula,
                140, // arrghus
                146, // helmasaur
                147, // idk
                149, 150, 151, 152, // eye lasers
                158, 159, 160, // idk
                162, // kholdstare
                171, 172, 173, 174, 175, 176, 177, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, // idk
                189, 190, // vitreous, eyeballs
                191, // aghanim lightning
                192, 193, // idk
                196, 197, 198, 200, // idk
                203, // trinexx
                206, // blind
                210, 213, // idk
                214, 215 // ganon
            };

            // we want bosses to take damage class 2
            int[] bosses = new int[]
            {
                83, // armos
                84, // lanmolas
                9, // moldorm
                122, // aghanim - maybe shouldn't be here but it's funny
                146, // helmasaur
                140, // arrghus
                136, 137, // mothula, mothula beam
                206, // blind
                162, // kholdstare
                189, 190, // vitreous, eyeballs
                203, // trinexx
                214, 215 // ganon
            };
            
            // which exception list to use depends on what mode -> what should be able to take level 1 damage
            int[] exceptionList;
            switch (mode)
            {
                case "full":
                    exceptionList = fullDmgExceptions;
                    break;
                default: // safe mode
                    exceptionList = normalDmgExceptions;
                    break;
            }

            // now to actually change the table
            int address;
            // each row is an enemy
            for (int enemyID=0; enemyID<216; enemyID++)
            {
                // kholdstare's shell and freezors use class 4 - must always take damage from fire rod and bombos because the game expects it
                if (enemyID == 163 || enemyID == 161)
                {

                    for (int weaponClass = 0; weaponClass < 16; weaponClass++)
                    {
                        address = weaponClass + enemyID * 16;
                        // set to take class 4 from all weapons. If the weapon has incinerate effect, or is fire rod or bombos, class 4 will do damage
                        table[address] = 4;
                    }
                    continue;
                }

                // if enemy is not in exception list, give it class 1 damage for all weapons
                if (!Array.Exists(exceptionList, element => element == enemyID))
                {
                    for (int weaponClass = 0; weaponClass < 16; weaponClass++)
                    {
                        address = weaponClass + enemyID * 16;
                        table[address] = 1;
                    }
                    continue;
                }

                // if it's a boss, assign it damage class 2 (the 'safe' damage class)
                // also slimes need to not take slime damage so lets just make them safe
                if (Array.Exists(bosses, element => element == enemyID) || enemyID == 143)
                {
                    for (int weaponClass = 0; weaponClass < 16; weaponClass++)
                    {
                        address = weaponClass + enemyID * 16;
                        table[address] = 2;
                    }
                    continue;
                }
                // anything that misses all conditions has either default all-zeros (takes no damage)...
                // ...or does not have a sprite. if you mess with these you deserve whatever happens.
            }
            return table;
        }

        /* make a brand new damage table
         * @return byte[128] new damage table
         */
        static byte[] Create_Damage_Table()
        {
            Random random = new Random();

            //// all possible damage values, and corresponding 'safe' values. length 13
            //// damage*5, fairy, stun--, stun-, incinerate, freeze, stun, slime
            //byte[] damages = new byte[] {0x01,0x02,0x04,0x08,0x10,0x64,0xF9,0xFB,0xFC,0xFD,0xFE,0xFF,0xFA};
            //byte[] safeDmg = new byte[] {0x01,0x02,0x04,0x08,0x10,0x64,0x04,0x01,0x01,0x08,0x04,0x02,0x04};

            // new concept: 16 values for 1:1 to the weapon classes, no "repeats" (some intentional). length 16, last 3 are new
            byte[] full_damages = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x64, 0xF9, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF, 0xFA, 0x04, 0x08, 0xF9};
            byte[] full_safeDmg = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x64, 0x04, 0x01, 0x01, 0x08, 0x04, 0x02, 0x04, 0x04, 0x08, 0x04};

            /* new concept 2:
             * 2 separate weapon pools of 8 (must both be size 8 right now)
             * 1 - boomerang, lv1, bow, hookshot, bombs, silvers, fire rod, ice rod
             * 2 - lv2, lv3, lv4, lv5, powder, bombos, ether, quake
             * each pool contains 4 pre-determined damage types, the remaining 4 are random
             */
            // indices of pool1, pool2 is everything else
            int[] pool1 = new int[] { 0, 1, 6, 7, 8, 9, 11, 12 };
            
            // required and remaining damage types for each pool (arbitrary, used for tweaking)
            byte[] pool1_req = new byte[] { 0x02, 0x04, 0xFD, 0xFF };
            byte[] pool1_rem = new byte[] { 0x00, 0x01, 0x08, 0x10, 0x64, 0xF9, 0xFA, 0xFB, 0xFC, 0xFE };

            byte[] pool2_req = new byte[] { 0x04, 0x08, 0xFC, 0xFE };
            byte[] pool2_rem = new byte[] { 0x00, 0x01, 0x02, 0x10, 0x64, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF };

            // shuffle the array to randomize what gets added
            pool1_rem = ArrayShuffle.Shuffle(pool1_rem);
            pool2_rem = ArrayShuffle.Shuffle(pool2_rem);

            // create damage type arrays
            byte[] pool1_dmg = new byte[8];
            byte[] pool2_dmg = new byte[8];
            // designed to accomodate changing requirement pool size
            for (int i = 0; i < pool1_req.Length; i++)
            {
                pool1_dmg[i] = pool1_req[i];
            }
            for (int i = pool1_req.Length; i < 8; i++)
            {
                pool1_dmg[i] = pool1_rem[i-pool1_req.Length];
            }
            // repeat for pool2
            for (int i = 0; i < pool2_req.Length; i++)
            {
                pool2_dmg[i] = pool2_req[i];
            }
            for (int i = pool2_req.Length; i < 8; i++)
            {
                pool2_dmg[i] = pool2_rem[i - pool2_req.Length];
            }

            // reshuffle combined arrays
            pool1_dmg = ArrayShuffle.Shuffle(pool1_dmg);
            pool2_dmg = ArrayShuffle.Shuffle(pool2_dmg);

            // create nice table to be returned
            byte[] table = new byte[128];

            // reused iterators
            byte dmgType;        // selected damage type for weapon class
            int pool1_index = 0; // the pools are made of interspersed weapon types,
            int pool2_index = 0; //    so the counters must move separately

            // there are 16 different weapon classes (rows)
            for (int weaponClass = 0; weaponClass < 16; weaponClass++)
            {
                // initializing all bytes to zero (instead of null) because most will be zero anyway. zeros will be overwritten
                for (int damageClass = 0; damageClass < 8; damageClass++)
                {
                    table[weaponClass * 8 + damageClass] = 0;
                }

                // select next damage type from weapon pool
                if (pool1.Contains(weaponClass))
                {
                    // weapon is pool 1
                    dmgType = pool1_dmg[pool1_index];
                    pool1_index++;
                }
                else
                {
                    // weapon is pool 2
                    dmgType = pool2_dmg[pool2_index];
                    pool2_index++;
                }

                // setting damage classes
                // damage class 0 (clink) always stays 00

                // set damage class 1 (regular damage)
                table[weaponClass * 8 + 1] = dmgType;

                // damage class 2 (equivalent safe damage) - mostly for bosses
                table[weaponClass * 8 + 2] = ConvertSafe(dmgType);

                // damage class 4 (melt damage) damages freezors and kholdstare's shell IF the regular damage is 'incinerate'
                if (dmgType == 0xFD)
                {
                    table[weaponClass * 8 + 4] = 0x08;
                }


                // WEAPON class 2 (level 2 sword damage) will always be damaging to avoid softlocks
                // replace damage classes 1 and 2 with damaging equivalent
                if (weaponClass == 2)
                {
                    table[weaponClass * 8 + 1] = ConvertDmg(dmgType);
                    table[weaponClass * 8 + 2] = ConvertSafeDmg(dmgType);
                }

                // weapon class 11 & 13 = damage class 4 for fire rod and bombos, which must be damaging against kholdstare's shell
                else if (weaponClass == 11) // fire rod
                {
                    // fire rod does 8 damage
                    table[weaponClass * 8 + 4] = 0x08;
                }
                else if (weaponClass == 13) // bombos
                {
                    // bombos always breaks shell
                    table[weaponClass * 8 + 4] = 0x64;
                }
            }
            return table;
        }

        // converts damage type to a safe type to use against bosses
        static byte ConvertSafe(byte damageType)
        {
            switch (damageType)
            {
                case 0xF9: // fairy
                case 0xFA: // slime
                    return 0x04;
                case 0xFD: // incinerate
                    return 0x08;
                case 0xFE: // freeze
                    return 0xFF; // med stun
                default:
                    return damageType;
                    
            }
        }

        // convert damage type to a type which does damage
        static byte ConvertDmg(byte damageType)
        {
            switch (damageType)
            {
                case 0x00:
                    return 0x01;
                case 0xFB:
                case 0xFC:
                    return 0x02;
                case 0xFE:
                    return 0x04;
                case 0xFF:
                    return 0x04;
                default:
                    return damageType;
            }
        }

        // convert damage type to a safe type which does damage
        static byte ConvertSafeDmg(byte damageType)
        {
            // fix things that do 0 damage
            damageType = ConvertSafe(damageType);
            // replace with damaging types
            switch (damageType)
            {
                case 0x00:
                    return 0x01;
                case 0xFB:
                case 0xFC:
                    return 0x02;
                case 0xFF:
                    return 0x04;
                default:
                    return damageType;
            }
        }

        // convert a byte to readable text if it is a special hex value
        static string HexToText(byte hexByte)
        {
            switch (hexByte)
            {
                // damages
                case 0xF9: return "Powder effect";
                case 0xFA: return "Slime Effect";
                case 0xFB: return "Short Stun";
                case 0xFC: return "Medium Stun";
                case 0xFD: return "Incinerate";
                case 0xFE: return "Freeze";
                case 0xFF: return "Long Stun";

                // fairy prizes
                case 0xE3: return "Fairy";
                case 0x79: return "Bees";
                case 0xAC: return "Apples";
                case 0xD2: return "Fish";
                case 0xD8: return "Heart";
                case 0xDA: return "Blue Rupee";
                case 0xDB: return "Red Rupee";
                case 0xDC: return "Bomb";
                case 0xDF: return "Small Magic";
                
                // whatever just make it a string
                default:
                    return Convert.ToString(hexByte);
            }
        }
    }
}
