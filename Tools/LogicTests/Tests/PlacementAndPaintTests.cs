using HouseFlip.Building;
using HouseFlip.Core;
using HouseFlip.Painting;
using HouseFlip.PhysicsGrab;
using NUnit.Framework;
using UnityEngine;

namespace HouseFlip.Tests
{
    /// <summary>Grid snapping (GDD 10), the paint palette (GDD 14) and carry weights (GDD 8).</summary>
    [TestFixture]
    public class PlacementAndPaintTests
    {
        private const float Cell = GameConstants.GridCellSize;

        // ------------------------------------------------------------------
        // Grid (GDD 10)
        // ------------------------------------------------------------------

        [Test]
        public void Snap_LandsOnCellBoundaries()
        {
            Vector3 snapped = GridUtility.Snap(new Vector3(1.24f, 0f, 2.76f));

            Assert.That(snapped.x % Cell, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(snapped.z % Cell, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Snap_LeavesHeightUntouched()
        {
            // The surface the ghost hit decides Y; snapping it would sink pieces into floors.
            Vector3 snapped = GridUtility.Snap(new Vector3(1.24f, 3.77f, 2.76f));

            Assert.That(snapped.y, Is.EqualTo(3.77f).Within(0.0001f));
        }

        [Test]
        public void SnapFootprint_EvenSizedItemsLandOnCellEdges()
        {
            // A 2x2 sofa should straddle cells symmetrically.
            Vector3 snapped = GridUtility.SnapFootprint(new Vector3(1.3f, 0f, 2.4f), new Vector2Int(2, 2));

            Assert.That(snapped.x % Cell, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(snapped.z % Cell, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SnapFootprint_OddSizedItemsLandOnCellCentres()
        {
            // A 1x1 lamp should sit in the middle of a cell, offset by half a cell.
            Vector3 snapped = GridUtility.SnapFootprint(new Vector3(1.3f, 0f, 2.4f), new Vector2Int(1, 1));

            float offsetX = Mathf.Abs(snapped.x % Cell);
            float offsetZ = Mathf.Abs(snapped.z % Cell);

            Assert.That(offsetX, Is.EqualTo(Cell * 0.5f).Within(0.0001f));
            Assert.That(offsetZ, Is.EqualTo(Cell * 0.5f).Within(0.0001f));
        }

        [Test]
        public void SnapFootprint_IsIdempotent()
        {
            // Re-snapping an already snapped point must not drift it, or holding the ghost
            // still would slowly walk the piece across the room.
            var footprint = new Vector2Int(3, 2);
            Vector3 once = GridUtility.SnapFootprint(new Vector3(4.37f, 0f, -2.11f), footprint);
            Vector3 twice = GridUtility.SnapFootprint(once, footprint);

            Assert.That(twice.x, Is.EqualTo(once.x).Within(0.0001f));
            Assert.That(twice.z, Is.EqualTo(once.z).Within(0.0001f));
        }

        [Test]
        public void SnapYaw_QuantisesToRightAngles()
        {
            Assert.That(GridUtility.SnapYaw(44f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(GridUtility.SnapYaw(46f), Is.EqualTo(90f).Within(0.001f));
            Assert.That(GridUtility.SnapYaw(181f), Is.EqualTo(180f).Within(0.001f));
            Assert.That(GridUtility.SnapYaw(-46f), Is.EqualTo(-90f).Within(0.001f));
        }

        [Test]
        public void SnapYaw_AccumulatesCleanlyOverRepeatedRotations()
        {
            // [R] adds 90 each press. Four presses must land back where it started,
            // with no drift creeping in through the rounding.
            float yaw = 37f;
            float snappedStart = GridUtility.SnapYaw(yaw);

            for (int i = 0; i < 4; i++)
            {
                yaw += 90f;
            }

            Assert.That(GridUtility.SnapYaw(yaw), Is.EqualTo(snappedStart + 360f).Within(0.001f));
        }

        [Test]
        public void SnapRotation_ProducesTheSnappedYaw()
        {
            Assert.That(GridUtility.SnapRotation(46f).eulerAngles.y, Is.EqualTo(90f).Within(0.01f));
            Assert.That(GridUtility.SnapRotation(181f).eulerAngles.y, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void FootprintToWorldSize_ScalesByCellSize()
        {
            Vector3 size = GridUtility.FootprintToWorldSize(new Vector2Int(4, 2), 1.5f);

            Assert.That(size.x, Is.EqualTo(4 * Cell).Within(0.0001f));
            Assert.That(size.z, Is.EqualTo(2 * Cell).Within(0.0001f));
            Assert.That(size.y, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void FootprintToWorldSize_TreatsZeroFootprintAsOneCell()
        {
            // A misconfigured asset should still produce a testable volume, not a
            // zero-extent box that overlaps nothing and can be placed inside a wall.
            Vector3 size = GridUtility.FootprintToWorldSize(Vector2Int.zero, 0f);

            Assert.That(size.x, Is.GreaterThan(0f));
            Assert.That(size.y, Is.GreaterThan(0f));
            Assert.That(size.z, Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------
        // Paint (GDD 14)
        // ------------------------------------------------------------------

        [Test]
        public void Palette_HasTheSevenMvpColours()
        {
            Assert.That(PaintColors.Count, Is.EqualTo(7), "GDD 14 lists seven MVP colours.");
            Assert.That(PaintColors.Names.Length, Is.EqualTo(PaintColors.Values.Length),
                "Every colour needs a name; a mismatch would throw when the picker builds.");
        }

        [Test]
        public void Palette_NamesMatchTheGdd()
        {
            Assert.That(PaintColors.Names,
                Is.EqualTo(new[] { "White", "Black", "Blue", "Red", "Green", "Yellow", "Pink" }));
        }

        [Test]
        public void Harmony_MatchingColoursAlwaysAgree()
        {
            for (int i = 0; i < PaintColors.Count; i++)
            {
                Assert.That(PaintColors.IsHarmonious(i, i), Is.True);
            }
        }

        [Test]
        public void Harmony_WhitePairsWithEverything()
        {
            for (int i = 0; i < PaintColors.Count; i++)
            {
                Assert.That(PaintColors.IsHarmonious(0, i), Is.True);
                Assert.That(PaintColors.IsHarmonious(i, 0), Is.True);
            }
        }

        [Test]
        public void Harmony_ClashingColoursDoNot()
        {
            const int blue = 2, red = 3;
            Assert.That(PaintColors.IsHarmonious(blue, red), Is.False);
        }

        [Test]
        public void Harmony_UnpaintedWallsNeverCount()
        {
            Assert.That(PaintColors.IsHarmonious(PaintColors.Unpainted, 0), Is.False);
            Assert.That(PaintColors.IsHarmonious(PaintColors.Unpainted, PaintColors.Unpainted), Is.False);
        }

        [Test]
        public void Palette_OutOfRangeIndexReturnsBarePlaster()
        {
            // Colour index arrives over the network, so it must be handled, not trusted.
            Assert.DoesNotThrow(() => PaintColors.Get(99));
            Assert.DoesNotThrow(() => PaintColors.Get(-7));
            Assert.That(PaintColors.GetName(99), Is.EqualTo("Bare"));
        }

        // ------------------------------------------------------------------
        // Carry weights (GDD 8)
        // ------------------------------------------------------------------

        [Test]
        public void OnlyHeavyObjectsNeedTwoPlayers()
        {
            Assert.That(MassCategory.Light.RequiredCarriers(), Is.EqualTo(1));
            Assert.That(MassCategory.Medium.RequiredCarriers(), Is.EqualTo(1));
            Assert.That(MassCategory.Heavy.RequiredCarriers(), Is.EqualTo(2));
        }

        [Test]
        public void MassIncreasesWithCategory()
        {
            Assert.That(MassCategory.Light.RigidbodyMass(),
                Is.LessThan(MassCategory.Medium.RigidbodyMass()));
            Assert.That(MassCategory.Medium.RigidbodyMass(),
                Is.LessThan(MassCategory.Heavy.RigidbodyMass()));
        }

        // ------------------------------------------------------------------
        // Tools (GDD 6, 13)
        // ------------------------------------------------------------------

        [Test]
        public void EveryToolHasADisplayName()
        {
            foreach (ToolType tool in System.Enum.GetValues(typeof(ToolType)))
            {
                Assert.That(tool.DisplayName(), Is.Not.Null.And.Not.Empty,
                    $"{tool} needs a name for the '(Need X)' prompt hints.");
            }
        }
    }
}
