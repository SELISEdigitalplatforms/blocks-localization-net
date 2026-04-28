using DomainService.Services;
using FluentAssertions;
using Xunit;
using ModuleService = DomainService.Services.Module;

namespace XUnitTest
{
    public class ModuleTests
    {
        [Fact]
        public void Module_CanCreateInstance()
        {
            // Act
            var module = new ModuleService();

            // Assert
            module.Should().NotBeNull();
        }

        [Fact]
        public void Module_ItemId_CanBeSetAndRetrieved()
        {
            // Arrange
            var module = new ModuleService();
            var testId = "test-module-id";

            // Act
            module.ItemId = testId;

            // Assert
            module.ItemId.Should().Be(testId);
        }

        [Fact]
        public void Module_ItemId_CanBeNull()
        {
            // Arrange
            var module = new ModuleService { ItemId = "initial-value" };

            // Act
            module.ItemId = null;

            // Assert
            module.ItemId.Should().BeNull();
        }

        [Fact]
        public void Module_ModuleName_CanBeSetAndRetrieved()
        {
            // Arrange
            var module = new ModuleService();
            var testName = "TestModule";

            // Act
            module.ModuleName = testName;

            // Assert
            module.ModuleName.Should().Be(testName);
        }

        [Fact]
        public void Module_ModuleName_WithEmptyString()
        {
            // Arrange
            var module = new ModuleService();

            // Act
            module.ModuleName = string.Empty;

            // Assert
            module.ModuleName.Should().Be(string.Empty);
        }

        [Fact]
        public void Module_AllProperties_CanBeSetTogether()
        {
            // Arrange
            var itemId = "module-123";
            var moduleName = "MyModule";

            // Act
            var module = new ModuleService
            {
                ItemId = itemId,
                ModuleName = moduleName
            };

            // Assert
            module.ItemId.Should().Be(itemId);
            module.ModuleName.Should().Be(moduleName);
        }

        [Fact]
        public void Module_Properties_PreservesDataTypes()
        {
            // Arrange
            var testId = "complex-id-with-special-chars-123_456";
            var testName = "Complex Module Name With Spaces";

            // Act
            var module = new ModuleService
            {
                ItemId = testId,
                ModuleName = testName
            };

            // Assert
            module.ItemId.Should().BeOfType<string>();
            module.ModuleName.Should().BeOfType<string>();
            module.ItemId.Should().Be(testId);
            module.ModuleName.Should().Be(testName);
        }

        [Fact]
        public void Module_ItemId_DefaultIsNull()
        {
            // Arrange & Act
            var module = new ModuleService();

            // Assert
            module.ItemId.Should().BeNull();
        }

        [Fact]
        public void Module_CanUpdateProperties_MultipleTimes()
        {
            // Arrange
            var module = new ModuleService();

            // Act
            module.ItemId = "id1";
            module.ModuleName = "Module1";
            
            var firstItemId = module.ItemId;
            var firstModuleName = module.ModuleName;

            module.ItemId = "id2";
            module.ModuleName = "Module2";
            
            var secondItemId = module.ItemId;
            var secondModuleName = module.ModuleName;

            // Assert
            firstItemId.Should().Be("id1");
            firstModuleName.Should().Be("Module1");
            secondItemId.Should().Be("id2");
            secondModuleName.Should().Be("Module2");
        }

        [Fact]
        public void Module_Instances_AreIndependent()
        {
            // Arrange & Act
            var module1 = new ModuleService { ItemId = "id1", ModuleName = "Module1" };
            var module2 = new ModuleService { ItemId = "id2", ModuleName = "Module2" };

            // Assert
            module1.ItemId.Should().Be("id1");
            module1.ModuleName.Should().Be("Module1");
            module2.ItemId.Should().Be("id2");
            module2.ModuleName.Should().Be("Module2");
            module1.ItemId.Should().NotBe(module2.ItemId);
            module1.ModuleName.Should().NotBe(module2.ModuleName);
        }
    }
}
