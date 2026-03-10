using Blocks.Genesis;
using FluentAssertions;

namespace XUnitTest
{
    public class LocalizationSecretTests
    {
        [Fact]
        public void Properties_CanBeAssignedAndRead()
        {
            var secret = new LocalizationSecret
            {
                ChatGptEncryptedSecret = "encrypted-value",
                ChatGptEncryptionKey = "encryption-key"
            };

            secret.ChatGptEncryptedSecret.Should().Be("encrypted-value");
            secret.ChatGptEncryptionKey.Should().Be("encryption-key");
        }

        [Fact]
        public void UpdateProperty_UpdatesWritableProperty()
        {
            var secret = new LocalizationSecret
            {
                ChatGptEncryptedSecret = "before"
            };

            LocalizationSecret.UpdateProperty(secret, nameof(LocalizationSecret.ChatGptEncryptedSecret), "after");

            secret.ChatGptEncryptedSecret.Should().Be("after");
        }

        [Fact]
        public void UpdateProperty_InvalidProperty_DoesNotThrowOrChangeValues()
        {
            var secret = new LocalizationSecret
            {
                ChatGptEncryptedSecret = "original",
                ChatGptEncryptionKey = "key"
            };

            var action = () => LocalizationSecret.UpdateProperty(secret, "MissingProperty", "value");

            action.Should().NotThrow();
            secret.ChatGptEncryptedSecret.Should().Be("original");
            secret.ChatGptEncryptionKey.Should().Be("key");
        }

        [Fact]
        public void ConvertValue_TargetTypeString_ReturnsOriginalString()
        {
            var result = LocalizationSecret.ConvertValue("sample", typeof(string));

            result.Should().Be("sample");
        }

        [Fact]
        public void ConvertValue_ValidConvertibleType_ReturnsConvertedValue()
        {
            var result = LocalizationSecret.ConvertValue("42", typeof(int));

            result.Should().Be(42);
            result.Should().BeOfType<int>();
        }

        [Fact]
        public void ConvertValue_InvalidConvertibleType_ReturnsOriginalString()
        {
            var result = LocalizationSecret.ConvertValue("not-a-number", typeof(int));

            result.Should().Be("not-a-number");
        }
    }
}
