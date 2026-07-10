using Doggiehood.Core.Decorations;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Graybox yard decoration at its Core-assigned position (#48).</summary>
    public sealed class DecorationView : MonoBehaviour
    {
        public Decoration Decoration { get; private set; }

        public static DecorationView Spawn(Decoration decoration, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Decoration - " + decoration.ItemName;
            go.transform.SetParent(parent);
            go.transform.localScale = new Vector3(1.4f, 0.35f, 1f);
            go.transform.position = new Vector3(decoration.YardPosition.X, 0.175f, decoration.YardPosition.Z);

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = ColorFor(decoration.ItemName);
            renderer.sharedMaterial = material;

            var view = go.AddComponent<DecorationView>();
            view.Decoration = decoration;
            return view;
        }

        private static Color ColorFor(string itemName)
        {
            switch (itemName)
            {
                case "bed": return CoreColors.FromHex("#6C4FC4");
                case "cushion": return CoreColors.FromHex("#FF6F61");
                case "blanket": return CoreColors.FromHex("#3FA7D6");
                default: return CoreColors.FromHex("#FFD23F");
            }
        }
    }
}
