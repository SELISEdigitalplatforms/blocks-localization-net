using DomainService.Shared.Events;
using FluentAssertions;
using Xunit;

namespace XUnitTest.Shared.Events
{
    public class GenerateUilmFilesEventTests
    {
        [Fact]
        public void Properties_CanBeAssignedAndRead()
        {
            var evt = new GenerateUilmFilesEvent
            {
                Guid = "guid-1",
                ModuleId = "mod-1",
                ProjectKey = "proj-1"
            };

            evt.Guid.Should().Be("guid-1");
            evt.ModuleId.Should().Be("mod-1");
            evt.ProjectKey.Should().Be("proj-1");
        }

        [Fact]
        public void OptionalProperties_DefaultToNull()
        {
            var evt = new GenerateUilmFilesEvent { Guid = "g" };

            evt.ModuleId.Should().BeNull();
            evt.ProjectKey.Should().BeNull();
        }

        [Fact]
        public void Record_ValueEquality_WhenAllPropertiesMatch()
        {
            var a = new GenerateUilmFilesEvent { Guid = "g", ModuleId = "m", ProjectKey = "p" };
            var b = new GenerateUilmFilesEvent { Guid = "g", ModuleId = "m", ProjectKey = "p" };

            a.Should().Be(b);
            a.GetHashCode().Should().Be(b.GetHashCode());
            (a == b).Should().BeTrue();
        }

        [Fact]
        public void Record_ValueInequality_WhenPropertiesDiffer()
        {
            var a = new GenerateUilmFilesEvent { Guid = "g1", ModuleId = "m", ProjectKey = "p" };
            var b = new GenerateUilmFilesEvent { Guid = "g2", ModuleId = "m", ProjectKey = "p" };

            a.Should().NotBe(b);
            (a != b).Should().BeTrue();
        }

        [Fact]
        public void Record_WithExpression_CreatesMutatedCopy()
        {
            var original = new GenerateUilmFilesEvent { Guid = "g", ModuleId = "m", ProjectKey = "p" };
            var copy = original with { ModuleId = "m2" };

            copy.Should().NotBeSameAs(original);
            copy.Guid.Should().Be("g");
            copy.ModuleId.Should().Be("m2");
            copy.ProjectKey.Should().Be("p");
            // original unchanged
            original.ModuleId.Should().Be("m");
        }

        [Fact]
        public void Record_ToString_IncludesAllPropertyValues()
        {
            var evt = new GenerateUilmFilesEvent { Guid = "g", ModuleId = "m", ProjectKey = "p" };

            var s = evt.ToString();

            s.Should().Contain("Guid = g");
            s.Should().Contain("ModuleId = m");
            s.Should().Contain("ProjectKey = p");
        }
    }
}
