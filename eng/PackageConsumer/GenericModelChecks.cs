using Cbor;
using Cbor.Samples;

internal static class GenericModelChecks
{
    internal static void Run(CborSerializerOptions options)
    {
        var box = new SampleBox<SampleDerived>(new SampleDerived(42, "ada") { Quantity = 7 });
        byte[] encoded = CborSerializer.Serialize(box, options);
        if (Convert.ToHexString(encoded) != "A100A300182A01634144410207")
        {
            throw new InvalidOperationException("Generic/inherited model wire contract changed.");
        }

        var decoded = CborSerializer.Deserialize<SampleBox<SampleDerived>>(encoded, options).Value;
        if (decoded.Id != 42 || decoded.Label != "ADA" || decoded.Quantity != 7 || decoded.Assignments != 1 ||
            CborSerializer.Deserialize<SampleBox<int>>(CborSerializer.Serialize(new SampleBox<int>(7), options), options).Value != 7 ||
            CborSerializer.Deserialize<SampleBox<string>>(CborSerializer.Serialize(new SampleBox<string>("水😀"), options), options).Value != "水😀")
        {
            throw new InvalidOperationException("Closed generic types or inherited construction failed.");
        }

        var tree = new SampleTree<int> { Value = 1, Children = [new() { Value = 2, Children = [] }] };
        var restored = CborSerializer.Deserialize<SampleTree<int>>(CborSerializer.Serialize(tree, options), options);
        if (restored.Value != 1 || restored.Children is not { Count: 1 } || restored.Children[0].Value != 2 ||
            restored.Children[0].Children is not { Count: 0 })
        {
            throw new InvalidOperationException("Recursive closed generic graph failed.");
        }
    }
}
