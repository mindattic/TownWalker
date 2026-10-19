using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>The lab's start screen: one row per traversal demo, each a playable proof of concept of the same loop.</summary>
    public static class LabMenu
    {
        public static void Build(VisualElement parent, LabApp app)
        {
            var bg = Ui.El("", parent);
            Ui.Fill(bg);
            bg.style.backgroundImage = new StyleBackground(LabArt.Background(1));
            bg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.45f, 0.55f);

            var col = Ui.El("col", parent);
            Ui.Fill(col);
            col.style.paddingTop = 90; col.style.paddingLeft = col.style.paddingRight = 40;

            var title = Ui.Text("TownWalker", "title shadow", col);
            Fonts.UseDisplay(title);
            title.style.fontSize = 96;
            var sub = Ui.Text("Traversal lab — how does the walking town cross the surface?", "small shadow", col);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.whiteSpace = WhiteSpace.Normal;
            var note = Ui.Text("Every demo plays the same loop: scan → choose a site → walk the town or send the ground team by bike → battle (placeholder) → haul fuel and ore home. Compare the feel, the speed to battle and the taps.", "small dim shadow", col);
            note.style.whiteSpace = WhiteSpace.Normal; note.style.marginTop = 20; note.style.marginBottom = 30;

            var walker = new WalkerRig2D(WalkerRig2D.View.Side) { name = "walker", Walking = true, PixelsPerUnit = 24f, Ground = 0.92f };
            walker.style.height = 560;
            col.Add(walker);

            var list = Ui.Window(col);
            foreach (var make in LabApp.Catalog)
            {
                var d = make();
                var row = Ui.El("card", list);
                row.name = "open-" + d.Id;
                var c = Ui.El("col grow", row);
                Ui.Text(d.Title, "h2", c);
                Ui.Text(d.Pitch, "tiny dim", c).style.whiteSpace = WhiteSpace.Normal;
                var id = d.Id;
                Ui.Button("Open", () => app.Open(id), "btn--small btn--primary", row).name = "btn-" + d.Id;
            }
        }
    }
}
