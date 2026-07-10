using Doggiehood.Core.Audio;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Audio
{
    public class AudioEventBusTests
    {
        [SetUp]
        public void ResetBus()
        {
            AudioEventBus.Reset();
        }

        [Test]
        public void DefinesTheMvpSfxEvents()
        {
            // #40: bark, delivery truck, item delivery, UI taps.
            Assert.That(System.Enum.GetNames(typeof(SfxEvent)), Is.SupersetOf(new[]
            {
                "Bark", "TruckArrival", "ItemDelivered", "UiTap", "UiConfirm",
            }));
        }

        [Test]
        public void Publish_ReachesSubscribers()
        {
            SfxEvent? received = null;
            AudioEventBus.Subscribe(e => received = e);

            AudioEventBus.Publish(SfxEvent.Bark);

            Assert.That(received, Is.EqualTo(SfxEvent.Bark));
        }

        [Test]
        public void PlaybackFailures_NeverBlockGameplay()
        {
            // #40: a throwing audio handler (missing clip etc.) must never
            // propagate an exception into game code.
            AudioEventBus.Subscribe(e => throw new System.InvalidOperationException("missing clip"));

            Assert.That(() => AudioEventBus.Publish(SfxEvent.TruckArrival), Throws.Nothing);
        }

        [Test]
        public void PublishWithNoSubscribers_IsSafe()
        {
            Assert.That(() => AudioEventBus.Publish(SfxEvent.UiTap), Throws.Nothing);
        }
    }
}
