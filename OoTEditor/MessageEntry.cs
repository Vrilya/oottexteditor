namespace OoTEditor;

/// <summary>
/// Represents a single message entry parsed from the OoT message table.
/// </summary>
public class MessageEntry
{
    public int Id { get; set; }
    public int Type { get; set; }
    public int Position { get; set; }
    public int Bank { get; set; }
    public int Offset { get; set; }
    public string Text { get; set; } = string.Empty;

    public MessageEntry(int id, int type, int position, int bank, int offset)
    {
        Id = id;
        Type = type;
        Position = position;
        Bank = bank;
        Offset = offset;
    }

    /// <summary>
    /// Returns the label shown in the message list (ID + short preview of text).
    /// </summary>
    public string Label()
    {
        string snippet = Text.Replace("\n", " ");
        if (snippet.Length > 30)
            snippet = snippet[..30] + "...";
        return $"0x{Id:x4}  {snippet}";
    }
}