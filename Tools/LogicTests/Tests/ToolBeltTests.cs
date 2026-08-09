using HouseFlip.Core;
using NUnit.Framework;

namespace HouseFlip.Tests
{
    /// <summary>
    /// The hotbar's slot order is shared by the UI that draws it and the mouse wheel that
    /// steps through it, so the two can only disagree if this arithmetic is wrong.
    /// </summary>
    [TestFixture]
    public class ToolBeltTests
    {
        [Test]
        public void Slots_StartWithBareHandsAndCoverEveryTool()
        {
            // Slot 0 is the number key that puts your tools away, and it has to line up
            // with the [0] binding in PlayerToolController.
            Assert.That(ToolBelt.Slots[0], Is.EqualTo(ToolType.None));

            foreach (ToolType tool in System.Enum.GetValues(typeof(ToolType)))
            {
                Assert.That(ToolBelt.Slots, Contains.Item(tool),
                    $"{tool} has no hotbar slot, so it can never be reached by wheel or number key.");
            }

            Assert.That(ToolBelt.Slots.Length, Is.EqualTo(System.Enum.GetValues(typeof(ToolType)).Length),
                "A duplicated slot would give one tool two number keys.");
        }

        [Test]
        public void IndexOf_MatchesTheNumberKeyPrintedOnTheSlot()
        {
            // The number keys are bound literally: [1] is the hammer, [4] the vacuum.
            Assert.That(ToolBelt.IndexOf(ToolType.None), Is.EqualTo(0));
            Assert.That(ToolBelt.IndexOf(ToolType.Hammer), Is.EqualTo(1));
            Assert.That(ToolBelt.IndexOf(ToolType.CleaningTool), Is.EqualTo(4));
            Assert.That(ToolBelt.IndexOf(ToolType.PaintRoller), Is.EqualTo(5));
        }

        [Test]
        public void Cycle_StepsOneSlotInEachDirection()
        {
            Assert.That(ToolBelt.Cycle(ToolType.Hammer, 1), Is.EqualTo(ToolType.Screwdriver));
            Assert.That(ToolBelt.Cycle(ToolType.Screwdriver, -1), Is.EqualTo(ToolType.Hammer));
        }

        [Test]
        public void Cycle_WrapsAtBothEnds()
        {
            // Scrolling left off slot 0 is the case that breaks with a bare % — C# keeps
            // the sign of the dividend, so it would index the array at -1.
            Assert.That(ToolBelt.Cycle(ToolType.None, -1), Is.EqualTo(ToolType.PaintRoller));
            Assert.That(ToolBelt.Cycle(ToolType.PaintRoller, 1), Is.EqualTo(ToolType.None));
        }

        [Test]
        public void Cycle_HandlesStepsLargerThanTheBelt()
        {
            int count = ToolBelt.Slots.Length;

            // A fast flick of the wheel reports several notches in one frame.
            Assert.That(ToolBelt.Cycle(ToolType.Wrench, count), Is.EqualTo(ToolType.Wrench));
            Assert.That(ToolBelt.Cycle(ToolType.Wrench, -count * 3), Is.EqualTo(ToolType.Wrench));
            Assert.That(ToolBelt.Cycle(ToolType.Hammer, count + 1), Is.EqualTo(ToolType.Screwdriver));
        }

        [Test]
        public void Cycle_NeverStandsStillOnASingleStep()
        {
            // Cycling is not the number-key toggle: every notch must land somewhere new,
            // or the wheel would silently put the tool away.
            foreach (ToolType tool in ToolBelt.Slots)
            {
                Assert.That(ToolBelt.Cycle(tool, 1), Is.Not.EqualTo(tool));
                Assert.That(ToolBelt.Cycle(tool, -1), Is.Not.EqualTo(tool));
            }
        }
    }
}
