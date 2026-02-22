using System;
using UnityEngine;

// Tests unitaires pour MinHeap<T>
// Exécutables depuis l'inspecteur Unity (clic droit sur le composant → choisir un test)
// MinHeap étant un POCO pur sans dépendances Unity, les résultats sont déterministes
public class MinHeapTests : MonoBehaviour
{
    // Nœud de test minimal implémentant IComparable
    // Classe (référence) plutôt que struct pour correspondre à l'usage réel avec PathNode
    private class TestNode : IComparable<TestNode>
    {
        public int value;

        public TestNode(int v) { value = v; }

        public int CompareTo(TestNode other) => value.CompareTo(other.value);

        public override string ToString() => $"Node({value})";
    }

    // === HELPER ===

    // Affiche ✅ ou ❌ et retourne le résultat pour chaîner les assertions
    private bool Assert(bool condition, string label, string detail = "")
    {
        string suffix = detail != "" ? $" — {detail}" : "";
        if (condition)
            Debug.Log($"  ✅ {label}{suffix}");
        else
            Debug.LogError($"  ❌ {label}{suffix}");
        return condition;
    }

    // === TESTS INDIVIDUELS ===

    // Valide que des insertions dans un ordre quelconque sont extraites en ordre croissant
    [ContextMenu("Test 1 — Insert + ExtractMin (ordre croissant)")]
    public void Test_InsertExtractMin_OrdreCorrect()
    {
        Debug.Log("─── Test 1 : Insert + ExtractMin ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();

        // Insertion dans un ordre non trié
        int[] insertValues = { 5, 3, 7, 1, 4 };
        foreach (int v in insertValues)
            heap.Insert(new TestNode(v));

        bool passed = Assert(heap.Count == 5, "Count == 5 après 5 insertions");

        // L'extraction doit retourner les valeurs triées 1→3→4→5→7
        int[] expectedOrder = { 1, 3, 4, 5, 7 };
        for (int i = 0; i < expectedOrder.Length; i++)
        {
            int extracted = heap.ExtractMin().value;
            passed &= Assert(
                extracted == expectedOrder[i],
                $"ExtractMin #{i + 1}",
                $"attendu {expectedOrder[i]}, obtenu {extracted}"
            );
        }

        passed &= Assert(heap.Count == 0, "Count == 0 après extraction complète");

        Debug.Log(passed ? "✅ Test 1 PASSÉ\n" : "❌ Test 1 ÉCHOUÉ\n");
    }

    // Valide que Peek retourne le minimum sans le retirer du tas
    [ContextMenu("Test 2 — Peek (lecture sans extraction)")]
    public void Test_Peek_SansExtraction()
    {
        Debug.Log("─── Test 2 : Peek ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        heap.Insert(new TestNode(5));
        heap.Insert(new TestNode(2));
        heap.Insert(new TestNode(8));

        bool passed = true;
        passed &= Assert(heap.Peek().value == 2, "Peek retourne 2 (minimum)", $"obtenu {heap.Peek().value}");
        passed &= Assert(heap.Count == 3,        "Count reste 3 après Peek (pas d'extraction)");

        // Peek stable après plusieurs appels
        passed &= Assert(heap.Peek().value == 2, "Peek retourne toujours 2 au 2e appel");

        Debug.Log(passed ? "✅ Test 2 PASSÉ\n" : "❌ Test 2 ÉCHOUÉ\n");
    }

    // Valide le comportement avec un seul élément inséré puis extrait
    [ContextMenu("Test 3 — Élément unique")]
    public void Test_ElementUnique()
    {
        Debug.Log("─── Test 3 : Élément unique ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        TestNode node = new TestNode(42);

        heap.Insert(node);
        bool passed = Assert(heap.Count == 1, "Count == 1 après Insert");

        int extracted = heap.ExtractMin().value;
        passed &= Assert(extracted == 42, "ExtractMin retourne 42");
        passed &= Assert(heap.Count == 0, "Count == 0 après ExtractMin");

        Debug.Log(passed ? "✅ Test 2 PASSÉ\n" : "❌ Test 2 ÉCHOUÉ\n");
    }

    // Valide que UpdateItem remonte correctement un nœud dont la valeur a diminué
    // Correspond au cas A* : gCost réduit → fCost réduit → nœud doit remonter dans le tas
    [ContextMenu("Test 4 — UpdateItem (diminution de valeur)")]
    public void Test_UpdateItem_Remonte()
    {
        Debug.Log("─── Test 4 : UpdateItem ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        TestNode nodeA = new TestNode(10);
        TestNode nodeB = new TestNode(5);
        TestNode nodeC = new TestNode(8);
        heap.Insert(nodeA); // fCost=10 → devrait sortir en 3e
        heap.Insert(nodeB); // fCost=5  → devrait sortir en 1er
        heap.Insert(nodeC); // fCost=8  → devrait sortir en 2e

        // Simule une mise à jour A* : nodeA trouve un chemin plus court (10→1)
        nodeA.value = 1;
        heap.UpdateItem(nodeA); // nodeA doit remonter en tête du tas

        bool passed = true;
        int first  = heap.ExtractMin().value;
        int second = heap.ExtractMin().value;
        int third  = heap.ExtractMin().value;

        passed &= Assert(first  == 1, "ExtractMin #1 == 1 (nodeA mis à jour)", $"obtenu {first}");
        passed &= Assert(second == 5, "ExtractMin #2 == 5 (nodeB)",             $"obtenu {second}");
        passed &= Assert(third  == 8, "ExtractMin #3 == 8 (nodeC)",             $"obtenu {third}");

        Debug.Log(passed ? "✅ Test 3 PASSÉ\n" : "❌ Test 3 ÉCHOUÉ\n");
    }

    // Valide que Contains retourne true après Add et false après ExtractMin
    [ContextMenu("Test 5 — Contains")]
    public void Test_Contains()
    {
        Debug.Log("─── Test 5 : Contains ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        TestNode node = new TestNode(7);

        bool passed = Assert(!heap.Contains(node), "Contains == false avant Insert");
        heap.Insert(node);
        passed &= Assert(heap.Contains(node),  "Contains == true après Insert");
        heap.ExtractMin();
        passed &= Assert(!heap.Contains(node), "Contains == false après ExtractMin");

        Debug.Log(passed ? "✅ Test 4 PASSÉ\n" : "❌ Test 4 ÉCHOUÉ\n");
    }

    // Valide qu'une extraction sur un tas vide lève InvalidOperationException
    [ContextMenu("Test 6 — ExtractMin sur tas vide (exception)")]
    public void Test_ExtractMin_TasVide_Exception()
    {
        Debug.Log("─── Test 6 : ExtractMin tas vide ───");

        MinHeap<TestNode> heap = new MinHeap<TestNode>();
        bool passed = false;

        try
        {
            heap.ExtractMin();
            Assert(false, "ExtractMin heap vide", "devrait lever InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            passed = Assert(true, "InvalidOperationException levée correctement");
        }
        catch (Exception ex)
        {
            Assert(false, "Exception inattendue", ex.GetType().Name);
        }

        Debug.Log(passed ? "✅ Test 5 PASSÉ\n" : "❌ Test 5 ÉCHOUÉ\n");
    }

    // === RUNNER GLOBAL ===

    // Lance tous les tests en séquence et affiche un bilan
    [ContextMenu("▶ Run All MinHeap Tests")]
    public void RunAllTests()
    {
        Debug.Log("══════════════════════════════════════");
        Debug.Log("      MinHeap — Run All Tests         ");
        Debug.Log("══════════════════════════════════════");

        Test_InsertExtractMin_OrdreCorrect();
        Test_Peek_SansExtraction();
        Test_ElementUnique();
        Test_UpdateItem_Remonte();
        Test_Contains();
        Test_ExtractMin_TasVide_Exception();

        Debug.Log("══════════════════════════════════════");
        Debug.Log("           Fin des tests              ");
        Debug.Log("══════════════════════════════════════");
    }
}
