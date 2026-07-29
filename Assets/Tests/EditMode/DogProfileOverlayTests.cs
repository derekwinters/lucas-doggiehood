using Doggiehood.Core.Dogs;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #165: the dog profile overlay, built under the #256 CanvasScaler and
    /// asserted against the approved wireframe's named constants
    /// (docs/specs/ui/dog-profile.md / mockups/dog-profile.html, #161/#177).
    /// Covers the centered card + scrim, the four data fields sourced from the
    /// Core <see cref="DogProfile"/>, the top-right close affordance, and the
    /// Home button that requests a camera fly-to that dog's house.
    /// </summary>
    public class DogProfileOverlayTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject overlayHost;
        private DogProfileOverlay overlay;

        [SetUp]
        public void CreateOverlay()
        {
            // #291: labels bind a bundled UI font via Resources.Load; force-import
            // it so a fresh CI Library resolves it before the overlay is built
            // (docs/engineering/unity-serialization.md §4).
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("dog-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            overlay = overlayHost.AddComponent<DogProfileOverlay>();
            overlay.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
        }

        private static Dog SampleDog()
        {
            // Bailey — Golden Retriever, Adventurous/Exploring, house 2, adult.
            return new Dog("Bailey", Breed.GoldenRetriever, Personality.AdventurousExploring, 2, false);
        }

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(DogProfileOverlay.ProfileWidthPx, Is.EqualTo(900f));
            Assert.That(DogProfileOverlay.ProfilePaddingPx, Is.EqualTo(48f));
            Assert.That(DogProfileOverlay.PortraitSizePx, Is.EqualTo(220f));
            Assert.That(DogProfileOverlay.CloseButtonSizePx, Is.EqualTo(72f));
            Assert.That(DogProfileOverlay.HomeButtonHeightPx, Is.EqualTo(96f),
                "the Home button reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void Card_IsCenteredAtTheWireframeWidth()
        {
            var rect = overlay.CardRect;

            Assert.That(rect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.ProfileWidthPx));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (ProfileAnchor = Center)");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void Scrim_StretchesAcrossTheWholeCanvas()
        {
            Assert.That(overlay.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void CloseButton_IsTheWireframeSizeAndAnchoredTopRight()
        {
            Assert.That(overlay.CloseButtonRect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.anchorMin, Is.EqualTo(Vector2.one),
                "the close affordance sits at the card's top-right corner");
            Assert.That(overlay.CloseButtonRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Portrait_IsTheWireframeSize()
        {
            Assert.That(overlay.PortraitRect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.PortraitSizePx));
            Assert.That(overlay.PortraitRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.PortraitSizePx));
        }

        [Test]
        public void HomeButton_IsTheSharedPillHeight()
        {
            Assert.That(overlay.HomeButtonRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.HomeButtonHeightPx));
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void Open_ShowsTheFourFields_ReadFromTheDogsCoreData()
        {
            overlay.Open(SampleDog());

            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(overlay.NameLabel.text, Is.EqualTo("Bailey"));
            Assert.That(overlay.BreedChipLabel.text, Is.EqualTo("Golden Retriever"));
            Assert.That(overlay.AgeValueLabel.text, Is.EqualTo("Adult"));
            Assert.That(overlay.PersonalityValueLabel.text, Is.EqualTo("Adventurous/Exploring"));
        }

        [Test]
        public void StatTiles_AreLabeledAgeAndPersonality()
        {
            overlay.Open(SampleDog());

            Assert.That(overlay.AgeKeyLabel.text, Is.EqualTo("Age"));
            Assert.That(overlay.PersonalityKeyLabel.text, Is.EqualTo("Personality"));
        }

        [Test]
        public void Close_HidesThePanel()
        {
            overlay.Open(SampleDog());
            overlay.Close();

            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void HomeButton_RequestsAFlyToThatDogsHouse_AndClosesTheProfile()
        {
            var requestedHouse = -1;
            overlay.HomeRequested += houseId => requestedHouse = houseId;

            overlay.Open(SampleDog());
            overlay.Home();

            Assert.That(requestedHouse, Is.EqualTo(2),
                "Home flies the camera to the tapped dog's house (#165)");
            Assert.That(overlay.IsOpen, Is.False,
                "the Home button closes the profile before the camera moves");
        }

        [Test]
        public void Labels_UseTheBundledFont_NotAnEditorOnlyBuiltinLookup()
        {
            overlay.Open(SampleDog());
            var font = overlay.NameLabel.font;

            Assert.That(font, Is.Not.Null,
                "the name label has no font — it would draw nothing in the Android build (#291)");
            Assert.That(font.name, Does.Contain("DejaVu"));
            Assert.That(font.name, Does.Not.Contain("Arial"));
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
        }
    }
}
