using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace OoTEditor;

/// <summary>
/// Handles all binary encoding/decoding and table parsing for OoT messages.
/// </summary>
public static partial class MessageCodec
{
    // --------------------------------------------------------
    // Character / command lookup tables
    // --------------------------------------------------------

    private static readonly Dictionary<byte, char> SpecialChars = new()
    {
        { 0x80, 'À' }, { 0x81, 'Á' }, { 0x82, 'Å' }, { 0x83, 'Ä' }, { 0x84, 'Ç' },
        { 0x85, 'È' }, { 0x86, 'É' }, { 0x87, 'Ê' }, { 0x88, 'Ë' }, { 0x89, 'Ï' },
        { 0x8a, 'Ô' }, { 0x8b, 'Ö' }, { 0x8c, 'Ù' }, { 0x8d, 'Û' }, { 0x8e, 'Ü' },
        { 0x8f, 'ß' }, { 0x90, 'à' }, { 0x91, 'á' }, { 0x92, 'å' }, { 0x93, 'ä' },
        { 0x94, 'ç' }, { 0x95, 'è' }, { 0x96, 'é' }, { 0x97, 'ê' }, { 0x98, 'ë' },
        { 0x99, 'ï' }, { 0x9a, 'ô' }, { 0x9b, 'ö' }, { 0x9c, 'ù' }, { 0x9d, 'û' },
        { 0x9e, 'ü' },
    };

    // Reverse map: character → byte
    private static readonly Dictionary<char, byte> CharEncode;

    private static readonly Dictionary<byte, string> ButtonBytes = new()
    {
        { 0x9f, "A-button" }, { 0xa0, "B-button" }, { 0xa1, "C-button" }, { 0xa2, "L-button" }, { 0xa3, "R-button" },
        { 0xa4, "Z-button" }, { 0xa5, "C-up"     }, { 0xa6, "C-down"   }, { 0xa7, "C-left"   }, { 0xa8, "C-right"  },
        { 0xa9, "Triangle" }, { 0xaa, "Stick"    },
    };
    private static readonly Dictionary<string, byte> ButtonEncode;

    private static readonly Dictionary<byte, string> ColorBytes = new()
    {
        { 0x40, "default"   }, { 0x41, "red"    }, { 0x42, "green"  }, { 0x43, "blue"  },
        { 0x44, "lightblue" }, { 0x45, "purple" }, { 0x46, "yellow" }, { 0x47, "black" },
    };
    private static readonly Dictionary<string, byte> ColorEncode;

    private static readonly Dictionary<byte, string> NoArgCmds = new()
    {
        { 0x0a, "shop"        }, { 0x0b, "event"        }, { 0x0d, "waitbutton"  }, { 0x0f, "name"       },
        { 0x08, "quicktexton" }, { 0x09, "quicktextoff" }, { 0x10, "ocarina"     }, { 0x11, "endfade"    },
        { 0x16, "marathon"    }, { 0x17, "horserace"    }, { 0x18, "archery"     }, { 0x19, "skulltulas" },
        { 0x1a, "unskippable" }, { 0x1b, "twochoice"    }, { 0x1c, "threechoice" }, { 0x1d, "fish"       },
        { 0x1f, "time"        }, { 0x04, "break"        },
    };
    private static readonly Dictionary<string, byte> NoArgEncode;

    // --------------------------------------------------------
    // Static constructor – build reverse maps
    // --------------------------------------------------------

    static MessageCodec()
    {
        CharEncode = new Dictionary<char, byte>();
        foreach (var kv in SpecialChars)
            CharEncode[kv.Value] = kv.Key;

        ButtonEncode = new Dictionary<string, byte>();
        foreach (var kv in ButtonBytes)
            ButtonEncode[kv.Value] = kv.Key;

        ColorEncode = new Dictionary<string, byte>();
        foreach (var kv in ColorBytes)
            ColorEncode[kv.Value] = kv.Key;

        NoArgEncode = new Dictionary<string, byte>();
        foreach (var kv in NoArgCmds)
            NoArgEncode[kv.Value] = kv.Key;
    }

    // --------------------------------------------------------
    // Decode
    // --------------------------------------------------------

    /// <summary>
    /// Convert raw bytes to human-readable shortcode text.
    /// </summary>
    public static string DecodeMessage(byte[] raw, int startOffset, int byteCount)
    {
        var result = new StringBuilder();
        int i = startOffset;
        int end = startOffset + byteCount;

        while (i < end)
        {
            byte b = raw[i];

            if (b == 0x00 || b == 0x03)
            {
                i++;
                continue;
            }
            else if (b == 0x02) // end marker
            {
                break;
            }
            else if (b == 0x01)
            {
                result.Append('\n');
            }
            else if (NoArgCmds.TryGetValue(b, out string? cmdName))
            {
                result.Append($"[{cmdName}]");
            }
            else if (b == 0x05) // color
            {
                i++;
                string color = ColorBytes.TryGetValue(raw[i], out string? c) ? c : $"{raw[i]:x2}";
                result.Append($"[color:{color}]");
            }
            else if (b == 0x06) // shift
            {
                i++;
                result.Append($"[shift:{raw[i]:x2}]");
            }
            else if (b == 0x07) // textid
            {
                int hi = raw[i + 1];
                int lo = raw[i + 2];
                i += 2;
                result.Append($"[textid:{(hi << 8 | lo):x4}]");
            }
            else if (b == 0x0c) // breakdelay
            {
                i++;
                result.Append($"[breakdelay:{raw[i]:x2}]");
            }
            else if (b == 0x0e) // fade
            {
                i++;
                result.Append($"[fade:{raw[i]:x2}]");
            }
            else if (b == 0x12) // sfx
            {
                int hi = raw[i + 1];
                int lo = raw[i + 2];
                i += 2;
                result.Append($"[sfx:{(hi << 8 | lo):x4}]");
            }
            else if (b == 0x13) // item
            {
                i++;
                result.Append($"[item:{raw[i]:x2}]");
            }
            else if (b == 0x14) // textspeed
            {
                i++;
                result.Append($"[textspeed:{raw[i]:x2}]");
            }
            else if (b == 0x15) // background
            {
                int a = raw[i + 1];
                int bb = raw[i + 2];
                int cc = raw[i + 3];
                i += 3;
                result.Append($"[background:{(a << 16 | bb << 8 | cc):x6}]");
            }
            else if (b == 0x1e) // minigame
            {
                i++;
                result.Append($"[minigame:{raw[i]:x2}]");
            }
            else if (SpecialChars.TryGetValue(b, out char specialCh))
            {
                result.Append(specialCh);
            }
            else if (ButtonBytes.TryGetValue(b, out string? btnName))
            {
                result.Append($"[{btnName}]");
            }
            else if (b >= 0x20 && b <= 0x7e)
            {
                result.Append((char)b);
            }

            i++;
        }

        return result.ToString();
    }

    // --------------------------------------------------------
    // Display helpers
    // --------------------------------------------------------

    [GeneratedRegex(@"\[breakdelay:[^\]]*\]")]
    private static partial Regex BreakDelayTag();

    [GeneratedRegex(@"\n?\[break\]\n?")]
    private static partial Regex BreakTagFull();

    [GeneratedRegex(@"\n?(\[breakdelay:[^\]]*\])\n?")]
    private static partial Regex BreakDelayTagFull();

    public static string ToDisplay(string text)
    {
        text = text.Replace("[break]", "\n[break]\n");
        text = BreakDelayTag().Replace(text, m => $"\n{m.Value}\n");
        return text;
    }

    public static string FromDisplay(string text)
    {
        text = BreakTagFull().Replace(text, "[break]");
        text = BreakDelayTagFull().Replace(text, "$1");
        return text;
    }

    // --------------------------------------------------------
    // Encode
    // --------------------------------------------------------

    /// <summary>
    /// Convert shortcode text back to raw bytes.
    /// </summary>
    public static byte[] EncodeMessage(string text)
    {
        var output = new List<byte>();
        int i = 0;

        while (i < text.Length)
        {
            char ch = text[i];

            if (ch == '[')
            {
                int j = text.IndexOf(']', i);
                if (j < 0) { i++; continue; }

                string token = text[(i + 1)..j];
                i = j + 1;

                string name, value;
                int colon = token.IndexOf(':');
                if (colon >= 0)
                {
                    name = token[..colon];
                    value = token[(colon + 1)..];
                }
                else
                {
                    name = token;
                    value = string.Empty;
                }

                if (NoArgEncode.TryGetValue(name, out byte noArgByte))
                {
                    output.Add(noArgByte);
                }
                else if (name == "color" && value.Length > 0)
                {
                    output.Add(0x05);
                    output.Add(ColorEncode.TryGetValue(value, out byte cb) ? cb : Convert.ToByte(value, 16));
                }
                else if (name == "shift" && value.Length > 0)
                {
                    output.Add(0x06);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (name == "textid" && value.Length > 0)
                {
                    output.Add(0x07);
                    int v = Convert.ToInt32(value, 16);
                    output.Add((byte)((v >> 8) & 0xff));
                    output.Add((byte)(v & 0xff));
                }
                else if (name == "breakdelay" && value.Length > 0)
                {
                    output.Add(0x0c);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (name == "fade" && value.Length > 0)
                {
                    output.Add(0x0e);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (name == "sfx" && value.Length > 0)
                {
                    output.Add(0x12);
                    int v = Convert.ToInt32(value, 16);
                    output.Add((byte)((v >> 8) & 0xff));
                    output.Add((byte)(v & 0xff));
                }
                else if (name == "item" && value.Length > 0)
                {
                    output.Add(0x13);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (name == "textspeed" && value.Length > 0)
                {
                    output.Add(0x14);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (name == "background" && value.Length > 0)
                {
                    output.Add(0x15);
                    int v = Convert.ToInt32(value, 16);
                    output.Add((byte)((v >> 16) & 0xff));
                    output.Add((byte)((v >> 8) & 0xff));
                    output.Add((byte)(v & 0xff));
                }
                else if (name == "minigame" && value.Length > 0)
                {
                    output.Add(0x1e);
                    output.Add(Convert.ToByte(value, 16));
                }
                else if (ButtonEncode.TryGetValue(name, out byte btnByte))
                {
                    output.Add(btnByte);
                }
            }
            else if (ch == '\n')
            {
                output.Add(0x01);
                i++;
            }
            else if (CharEncode.TryGetValue(ch, out byte specialByte))
            {
                output.Add(specialByte);
                i++;
            }
            else
            {
                output.Add((byte)ch);
                i++;
            }
        }

        output.Add(0x02);

        while (output.Count % 4 != 0)
            output.Add(0x00);

        return output.ToArray();
    }

    // --------------------------------------------------------
    // Table parsing
    // --------------------------------------------------------

    /// <summary>
    /// Parse the message table and return a list of MessageEntry objects with decoded text.
    /// </summary>
    public static List<MessageEntry> ParseTable(byte[] tblRaw, byte[] messageBytes)
    {
        var entries = new List<MessageEntry>();

        // Find English section (bank == 0x07)
        int i = 0;
        while (i + 4 < tblRaw.Length && tblRaw[i + 4] != 0x07)
            i += 8;

        // Read all entries until 0xfffd
        while (i < tblRaw.Length)
        {
            int id = (tblRaw[i] << 8) | tblRaw[i + 1];
            int type = (tblRaw[i + 2] >> 4) & 0x0f;
            int pos = tblRaw[i + 2] & 0x0f;
            int bank = tblRaw[i + 4];
            int offs = (tblRaw[i + 5] << 16) | (tblRaw[i + 6] << 8) | tblRaw[i + 7];

            entries.Add(new MessageEntry(id, type, pos, bank, offs));
            i += 8;

            if (id == 0xfffd)
                break;
        }

        // Decode texts (skip last sentinel entry)
        for (int idx = 0; idx < entries.Count - 1; idx++)
        {
            var e = entries[idx];
            int byteCount = entries[idx + 1].Offset - e.Offset;
            e.Text = DecodeMessage(messageBytes, e.Offset, byteCount);
        }

        return entries[..^1];
    }

    // --------------------------------------------------------
    // Build output files
    // --------------------------------------------------------

    /// <summary>
    /// Re-encode all entries and return (tableBytes, msgBytes).
    /// </summary>
    public static (byte[] tableBytes, byte[] msgBytes) BuildFiles(List<MessageEntry> entries)
    {
        var msgOut = new List<byte>();
        var newOffsets = new List<int>();

        foreach (var e in entries)
        {
            newOffsets.Add(msgOut.Count);
            msgOut.AddRange(EncodeMessage(e.Text));
        }

        int sentinelOffset = msgOut.Count;

        var tblOut = new List<byte>();

        void WriteEntry(int id, int type, int position, int bank, int offset)
        {
            tblOut.Add((byte)((id >> 8) & 0xff));
            tblOut.Add((byte)(id & 0xff));
            tblOut.Add((byte)(((type & 0x0f) << 4) | (position & 0x0f)));
            tblOut.Add(0x00);
            tblOut.Add((byte)bank);
            tblOut.Add((byte)((offset >> 16) & 0xff));
            tblOut.Add((byte)((offset >> 8) & 0xff));
            tblOut.Add((byte)(offset & 0xff));
        }

        for (int idx = 0; idx < entries.Count; idx++)
        {
            var e = entries[idx];
            WriteEntry(e.Id, e.Type, e.Position, e.Bank, newOffsets[idx]);
        }

        WriteEntry(0xfffd, 0x00, 0x00, 0x07, sentinelOffset);
        WriteEntry(0xffff, 0x00, 0x00, 0x00, 0x000000);

        return (tblOut.ToArray(), msgOut.ToArray());
    }
}