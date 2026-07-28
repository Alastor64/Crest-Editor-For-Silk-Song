using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongHelper;

public sealed class CustomCharm
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新建纹章";
    public string Description { get; set; } = "";

    public string? SlotCrestId { get; set; }

    public Dictionary<string, string> PartCrestIds { get; set; } = new();

    public bool IsEquipped { get; set; }

    public int SlotCount => CrestCatalog.ById(SlotCrestId)?.SlotCount ?? 0;

    public bool IsComplete
        => !string.IsNullOrEmpty(SlotCrestId)
           && CharmPartNames.NonSlotParts.All(p => PartCrestIds.ContainsKey(p.ToString()));

    public CustomCharm Clone()
    {
        return new CustomCharm
        {
            Id = Id,
            Name = Name,
            Description = Description,
            SlotCrestId = SlotCrestId,
            PartCrestIds = new Dictionary<string, string>(PartCrestIds),
        };
    }
}
