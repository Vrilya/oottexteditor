using System;
using System.Collections.Generic;
using System.Text;

namespace OoTEditor;

/// <summary>
/// Exports the current message entries to a Ship of Harkinian compatible C header format.
/// </summary>
public static class SohExporter
{
    // --------------------------------------------------------
    // Lookup tables
    // --------------------------------------------------------

    private static readonly Dictionary<int, string> BoxTypeStr = new()
    {
        { 0,    "TEXTBOX_TYPE_BLACK"        },
        { 1,    "TEXTBOX_TYPE_WOODEN"       },
        { 2,    "TEXTBOX_TYPE_BLUE"         },
        { 3,    "TEXTBOX_TYPE_OCARINA"      },
        { 4,    "TEXTBOX_TYPE_NONE_BOTTOM"  },
        { 5,    "TEXTBOX_TYPE_NONE_NO_SHADOW" },
        { 0xB,  "TEXTBOX_TYPE_CREDITS"      },
    };

    private static readonly Dictionary<int, string> BoxPosStr = new()
    {
        { 0, "TEXTBOX_POS_VARIABLE" },
        { 1, "TEXTBOX_POS_TOP"      },
        { 2, "TEXTBOX_POS_MIDDLE"   },
        { 3, "TEXTBOX_POS_BOTTOM"   },
    };

    private static readonly Dictionary<int, string> ColorStr = new()
    {
        { 0x40, "DEFAULT"    },
        { 0x41, "RED"        },
        { 0x42, "ADJUSTABLE" },
        { 0x43, "BLUE"       },
        { 0x44, "LIGHTBLUE"  },
        { 0x45, "PURPLE"     },
        { 0x46, "YELLOW"     },
        { 0x47, "BLACK"      },
    };

    private static readonly Dictionary<int, string> HighscoreStr = new()
    {
        { 0, "HS_HORSE_ARCHERY" },
        { 1, "HS_POE_POINTS"    },
        { 2, "HS_LARGEST_FISH"  },
        { 3, "HS_HORSE_RACE"    },
        { 4, "HS_MARATHON"      },
        { 5, "HS_UNK_05"        },
        { 6, "HS_DAMPE_RACE"    },
    };

    private static readonly Dictionary<byte, string> ExtractionCharmap = new()
    {
        { 0x80, "À"           }, { 0x81, "î"           }, { 0x82, "Â"           },
        { 0x83, "Ä"           }, { 0x84, "Ç"           }, { 0x85, "È"           },
        { 0x86, "É"           }, { 0x87, "Ê"           }, { 0x88, "Ë"           },
        { 0x89, "Ï"           }, { 0x8A, "Ô"           }, { 0x8B, "Ö"           },
        { 0x8C, "Ù"           }, { 0x8D, "Û"           }, { 0x8E, "Ü"           },
        { 0x8F, "ß"           }, { 0x90, "à"           }, { 0x91, "á"           },
        { 0x92, "â"           }, { 0x93, "ä"           }, { 0x94, "ç"           },
        { 0x95, "è"           }, { 0x96, "é"           }, { 0x97, "ê"           },
        { 0x98, "ë"           }, { 0x99, "ï"           }, { 0x9A, "ô"           },
        { 0x9B, "ö"           }, { 0x9C, "ù"           }, { 0x9D, "û"           },
        { 0x9E, "ü"           }, { 0x9F, "[A]"         }, { 0xA0, "[B]"         },
        { 0xA1, "[C]"         }, { 0xA2, "[L]"         }, { 0xA3, "[R]"         },
        { 0xA4, "[Z]"         }, { 0xA5, "[C-Up]"      }, { 0xA6, "[C-Down]"    },
        { 0xA7, "[C-Left]"    }, { 0xA8, "[C-Right]"   }, { 0xA9, "▼"           },
        { 0xAA, "[Control-Pad]" }, { 0xAB, "[D-Pad]"   },
    };

    // --------------------------------------------------------
    // Control code table  (tokType, macroName, argFmt, formatters)
    // argFmt chars: 'b' = 1 byte, 'h' = 2 bytes big-endian, 'x' = skip 1 byte
    // --------------------------------------------------------

    private static readonly Dictionary<byte, (string TokType, string Name, string ArgFmt, Func<int, string>[]? Fmts)> ControlCodes;

    static SohExporter()
    {
        static string FmtByte(int c)      => $"\"\\x{c:X2}\"";
        static string FmtTwoBytes(int c)  => $"\"\\x{(c >> 8) & 0xFF:X2}\\x{c & 0xFF:X2}\"";
        static string FmtColor(int c)     => ColorStr.TryGetValue(c, out var s) ? s : $"0x{c:02X}";
        static string FmtHighscore(int c) => HighscoreStr.TryGetValue(c, out var s) ? s : $"{c}";

        ControlCodes = new()
        {
            { 0x01, ("NEWLINE",            "NEWLINE",            "",    null) },
            { 0x02, ("END",                "END",                "",    null) },
            { 0x04, ("BOX_BREAK",          "BOX_BREAK",          "",    null) },
            { 0x05, ("COLOR",              "COLOR",              "b",   [FmtColor    ]) },
            { 0x06, ("SHIFT",              "SHIFT",              "b",   [FmtByte     ]) },
            { 0x07, ("TEXTID",             "TEXTID",             "h",   [FmtTwoBytes ]) },
            { 0x08, ("QUICKTEXT_ENABLE",   "QUICKTEXT_ENABLE",   "",    null) },
            { 0x09, ("QUICKTEXT_DISABLE",  "QUICKTEXT_DISABLE",  "",    null) },
            { 0x0A, ("PERSISTENT",         "PERSISTENT",         "",    null) },
            { 0x0B, ("EVENT",              "EVENT",              "",    null) },
            { 0x0C, ("BOX_BREAK_DELAYED",  "BOX_BREAK_DELAYED",  "b",   [FmtByte     ]) },
            { 0x0D, ("AWAIT_BUTTON_PRESS", "AWAIT_BUTTON_PRESS", "",    null) },
            { 0x0E, ("FADE",               "FADE",               "b",   [FmtByte     ]) },
            { 0x0F, ("NAME",               "NAME",               "",    null) },
            { 0x10, ("OCARINA",            "OCARINA",            "",    null) },
            { 0x11, ("FADE2",              "FADE2",              "h",   [FmtByte     ]) },
            { 0x12, ("SFX",                "SFX",                "h",   [FmtTwoBytes ]) },
            { 0x13, ("ITEM_ICON",          "ITEM_ICON",          "b",   [FmtByte     ]) },
            { 0x14, ("TEXT_SPEED",         "TEXT_SPEED",         "b",   [FmtByte     ]) },
            { 0x15, ("BACKGROUND",         "BACKGROUND",         "bbb", [FmtByte, FmtByte, FmtByte]) },
            { 0x16, ("MARATHON_TIME",      "MARATHON_TIME",      "",    null) },
            { 0x17, ("RACE_TIME",          "RACE_TIME",          "",    null) },
            { 0x18, ("POINTS",             "POINTS",             "",    null) },
            { 0x19, ("TOKENS",             "TOKENS",             "",    null) },
            { 0x1A, ("UNSKIPPABLE",        "UNSKIPPABLE",        "",    null) },
            { 0x1B, ("TWO_CHOICE",         "TWO_CHOICE",         "",    null) },
            { 0x1C, ("THREE_CHOICE",       "THREE_CHOICE",       "",    null) },
            { 0x1D, ("FISH_INFO",          "FISH_INFO",          "",    null) },
            { 0x1E, ("HIGHSCORE",          "HIGHSCORE",          "b",   [FmtHighscore]) },
            { 0x1F, ("TIME",               "TIME",               "",    null) },
        };
    }

    // --------------------------------------------------------
    // Public API
    // --------------------------------------------------------

    /// <summary>
    /// Produces a Ship of Harkinian compatible .h file from the given message entries.
    /// </summary>
    public static string Export(List<MessageEntry> entries)
    {
        var parts = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            // Re-encode entry to get raw bytes (captures any unsaved edits)
            byte[] raw = MessageCodec.EncodeMessage(entry.Text);
            string decoded = DecodeMessageSoh(raw);

            string boxType = BoxTypeStr.TryGetValue(entry.Type,     out var bt) ? bt : $"TEXTBOX_TYPE_UNK_{entry.Type:X}";
            string boxPos  = BoxPosStr .TryGetValue(entry.Position, out var bp) ? bp : $"TEXTBOX_POS_UNK_{entry.Position:X}";

            parts.Add($"DEFINE_MESSAGE(0x{entry.Id:X4}, {boxType}, {boxPos},\n{decoded}\n)\n");
        }

        return string.Join("\n", parts);
    }

    // --------------------------------------------------------
    // Decoder
    // --------------------------------------------------------

    private static string DecodeMessageSoh(byte[] msg)
    {
        if (msg.Length == 0)
            return "None";

        // Strip trailing 0x00 padding
        int end = msg.Length;
        while (end > 0 && msg[end - 1] == 0x00)
            end--;

        if (end == 0)
            return "None";

        // Pop the END byte (0x02) from the back
        end--;

        // Tokenise
        var tokens = new List<(string TokType, string Data)>();
        var textRun = new StringBuilder();
        int i = 0;

        void FlushText()
        {
            if (textRun.Length > 0)
            {
                tokens.Add(("TEXT", textRun.ToString()));
                textRun.Clear();
            }
        }

        while (i < end)
        {
            byte b = msg[i++];

            if (ControlCodes.TryGetValue(b, out var ctrl))
            {
                FlushText();
                string data = DecodeCtrl(msg, ref i, ctrl.Name, ctrl.ArgFmt, ctrl.Fmts);
                tokens.Add((ctrl.TokType, data));
            }
            else if (ExtractionCharmap.TryGetValue(b, out string? ch))
            {
                textRun.Append(ch);
            }
            else
            {
                // Printable ASCII pass-through; escape embedded quotes
                char c = (char)b;
                textRun.Append(c == '"' ? "\\\"" : c.ToString());
            }
        }

        FlushText();

        return EmitTokens(tokens);
    }

    private static string DecodeCtrl(byte[] msg, ref int i, string name, string argFmt, Func<int, string>[]? fmts)
    {
        if (argFmt.Length == 0)
            return name;

        var args = new List<int>();

        foreach (char a in argFmt)
        {
            switch (a)
            {
                case 'x': i++; break;          // skip padding byte
                case 'b': args.Add(msg[i++]); break;
                case 'h':
                    args.Add((msg[i] << 8) | msg[i + 1]);
                    i += 2;
                    break;
            }
        }

        var formatted = new string[args.Count];
        for (int k = 0; k < args.Count; k++)
            formatted[k] = fmts![k](args[k]);

        return $"{name}({string.Join(", ", formatted)})";
    }

    /// <summary>
    /// Formats a token list into the C string literal style used by SoH.
    /// </summary>
    private static string EmitTokens(List<(string TokType, string Data)> tokens)
    {
        if (tokens.Count == 0)
            return "\"\"";

        var sb = new StringBuilder();
        bool qState = false;  // currently inside an open "
        bool sState = false;  // need a space before next non-break token

        void MaybeEnterQ()
        {
            if (!qState) { sb.Append('"'); qState = true; }
        }

        void MaybeExitQ(bool space = false)
        {
            if (qState)
            {
                sb.Append('"');
                if (space) sb.Append(' ');
                qState = false;
            }
        }

        foreach (var (tokType, tokDat) in tokens)
        {
            if (tokType is "BOX_BREAK" or "BOX_BREAK_DELAYED")
            {
                MaybeExitQ();
                sState = false;
                sb.Append('\n');
                sb.Append(tokDat);
                sb.Append('\n');
                continue;
            }

            if (sState) { sb.Append(' '); sState = false; }

            if (tokType == "NEWLINE")
            {
                MaybeEnterQ();
                sb.Append("\\n\"\n");
                qState = false;
            }
            else if (tokType == "TEXT")
            {
                MaybeEnterQ();
                sb.Append(tokDat);
            }
            else
            {
                MaybeExitQ(space: true);
                sb.Append(tokDat);
                if (tokType is "TWO_CHOICE" or "THREE_CHOICE")
                    sb.Append('\n');
                else
                    sState = true;
            }
        }

        MaybeExitQ();

        string result = sb.ToString();
        if (result.Length > 0 && result[^1] == '\n')
            result = result[..^1];

        return result;
    }
}
