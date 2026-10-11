using System;
using NUnit.Framework;

// Tests EditMode du tas minimal pur utilisé par le pathfinding.
public class MinHeapTests
{
    private sealed class TestNode : IComparable<TestNode>
    {
        public int value;

        public TestNode(int value)
        {
            this.value = value;
        }

        public int CompareTo(TestNode other)
        {
            return value.CompareTo(other.value);
        }
    }

    // Vérifie que les extractions suivent l'ordre croissant.
    [Test]
    public void ExtractMin_UnsortedInsertions_ReturnsAscendingOrder()
    {
        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        foreach (int value in new[] { 5, 3, 7, 1, 4 })
            heap.Insert(new TestNode(value));

        Assert.AreEqual(5, heap.Count);
        CollectionAssert.AreEqual(new[] { 1, 3, 4, 5, 7 }, ExtractAll(heap));
        Assert.AreEqual(0, heap.Count);
    }

    // Vérifie que Peek ne retire pas le minimum.
    [Test]
    public void Peek_DoesNotRemoveMinimum()
    {
        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        heap.Insert(new TestNode(5));
        heap.Insert(new TestNode(2));
        heap.Insert(new TestNode(8));

        Assert.AreEqual(2, heap.Peek().value);
        Assert.AreEqual(2, heap.Peek().value);
        Assert.AreEqual(3, heap.Count);
    }

    // Vérifie qu'une diminution de valeur remonte l'élément.
    [Test]
    public void UpdateItem_DecreasedValue_MovesItemToRoot()
    {
        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        TestNode updated = new TestNode(10);
        heap.Insert(updated);
        heap.Insert(new TestNode(5));
        heap.Insert(new TestNode(8));

        updated.value = 1;
        heap.UpdateItem(updated);

        CollectionAssert.AreEqual(new[] { 1, 5, 8 }, ExtractAll(heap));
    }

    // Vérifie l'index de présence avant et après extraction.
    [Test]
    public void Contains_TracksInsertedAndExtractedItem()
    {
        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        TestNode node = new TestNode(7);

        Assert.IsFalse(heap.Contains(node));
        heap.Insert(node);
        Assert.IsTrue(heap.Contains(node));
        heap.ExtractMin();
        Assert.IsFalse(heap.Contains(node));
    }

    // Vérifie les erreurs explicites sur un tas vide.
    [Test]
    public void EmptyHeap_PeekAndExtractMin_Throw()
    {
        MinHeap<TestNode> heap = new MinHeap<TestNode>();

        Assert.Throws<InvalidOperationException>(() => heap.Peek());
        Assert.Throws<InvalidOperationException>(() => heap.ExtractMin());
    }

    // Extrait toutes les valeurs du tas pour simplifier les assertions d'ordre.
    private static int[] ExtractAll(MinHeap<TestNode> heap)
    {
        int[] values = new int[heap.Count];
        for (int index = 0; index < values.Length; index++)
            values[index] = heap.ExtractMin().value;
        return values;
    }
}
