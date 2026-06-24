using FluentAssertions;
using Prueba.Infrastructure.Services;

namespace Prueba.UnitTests.Infrastructure.Services;

public class GoogleVisionOcrServiceTests
{
    [Fact]
    public void ExtractNames_WithSpanishLabels_ShouldExtract()
    {
        var text = "Nombres: Juan Carlos\nApellidos: García López";

        var (names, confidence) = GoogleVisionOcrService.ExtractNames(text);

        names.Should().Be("Juan Carlos");
        confidence.Should().Be(0.85);
    }

    [Fact]
    public void ExtractNames_WithEnglishLabels_ShouldExtract()
    {
        var text = "Given Name: John\nSurname: Smith";

        var (names, confidence) = GoogleVisionOcrService.ExtractNames(text);

        names.Should().Be("John");
        confidence.Should().Be(0.85);
    }

    [Fact]
    public void ExtractNames_WithNoMatch_ShouldReturnEmpty()
    {
        var text = "Some random text without names";

        var (names, confidence) = GoogleVisionOcrService.ExtractNames(text);

        names.Should().BeEmpty();
        confidence.Should().Be(0.0);
    }

    [Fact]
    public void ExtractSurnames_WithSpanishLabels_ShouldExtract()
    {
        var text = "Apellidos: García López\nNombres: Juan";

        var (surnames, confidence) = GoogleVisionOcrService.ExtractSurnames(text);

        surnames.Should().Be("García López");
        confidence.Should().Be(0.85);
    }

    [Fact]
    public void ExtractSurnames_WithEnglishLabels_ShouldExtract()
    {
        var text = "Last Name: Smith\nName: John";

        var (surnames, confidence) = GoogleVisionOcrService.ExtractSurnames(text);

        surnames.Should().Be("Smith");
        confidence.Should().Be(0.85);
    }

    [Fact]
    public void ExtractDocumentNumber_WithDniLabel_ShouldExtract()
    {
        var text = "DNI: 40123456\nNombre: Ana";

        var (docNumber, confidence) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().Be("40123456");
        confidence.Should().Be(0.90);
    }

    [Fact]
    public void ExtractDocumentNumber_WithCedulaLabel_ShouldExtract()
    {
        var text = "Cédula: 1234567890\nNombres: María";

        var (docNumber, confidence) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().Be("1234567890");
        confidence.Should().Be(0.90);
    }

    [Fact]
    public void ExtractDocumentNumber_WithDocumentNoLabel_ShouldExtract()
    {
        var text = "Document No: AB123456\nName: John";

        var (docNumber, confidence) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().Be("AB123456");
        confidence.Should().Be(0.90);
    }

    [Fact]
    public void ExtractDocumentNumber_WithFallbackDigits_ShouldExtractWithLowerConfidence()
    {
        var text = "Some random text 12345678 more text";

        var (docNumber, confidence) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().Be("12345678");
        confidence.Should().Be(0.60);
    }

    [Fact]
    public void ExtractDocumentNumber_WithNoMatch_ShouldReturnEmpty()
    {
        var text = "No document number here";

        var (docNumber, confidence) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().BeEmpty();
        confidence.Should().Be(0.0);
    }

    [Fact]
    public void ExtractDateOfBirth_WithStandardFormat_ShouldParse()
    {
        var text = "Date of Birth: 1992-03-25\nName: Carlos";

        var (dob, confidence) = GoogleVisionOcrService.ExtractDateOfBirth(text);

        dob.Should().Be(new DateTime(1992, 3, 25));
        confidence.Should().Be(0.88);
    }

    [Fact]
    public void ExtractDateOfBirth_WithSpanishLabel_ShouldParse()
    {
        var text = "Fecha de Nacimiento: 1985-06-20\nNombres: Carlos";

        var (dob, confidence) = GoogleVisionOcrService.ExtractDateOfBirth(text);

        dob.Should().Be(new DateTime(1985, 6, 20));
        confidence.Should().Be(0.88);
    }

    [Fact]
    public void ExtractDateOfBirth_WithSlashFormat_ShouldParse()
    {
        var text = "Nacimiento: 25/03/1992\nNombres: Carlos";

        var (dob, confidence) = GoogleVisionOcrService.ExtractDateOfBirth(text);

        dob.Should().Be(new DateTime(1992, 3, 25));
        confidence.Should().Be(0.80);
    }

    [Fact]
    public void ExtractDateOfBirth_WithDashFormat_ShouldParse()
    {
        var text = "DOB: 20-06-1985\nName: Carlos";

        var (dob, confidence) = GoogleVisionOcrService.ExtractDateOfBirth(text);

        dob.Should().Be(new DateTime(1985, 6, 20));
        confidence.Should().Be(0.80);
    }

    [Fact]
    public void ExtractDateOfBirth_WithNoMatch_ShouldReturnNull()
    {
        var text = "No date of birth here";

        var (dob, confidence) = GoogleVisionOcrService.ExtractDateOfBirth(text);

        dob.Should().BeNull();
        confidence.Should().Be(0.0);
    }

    [Fact]
    public void ExtractNames_WithAccentedCharacters_ShouldExtract()
    {
        var text = "Nombres: María José\nApellidos: Pérez García";

        var (names, _) = GoogleVisionOcrService.ExtractNames(text);

        names.Should().Be("María José");
    }

    [Fact]
    public void ExtractDocumentNumber_WithHyphenatedNumber_ShouldExtract()
    {
        var text = "ID No: ABC-123456\nName: John";

        var (docNumber, _) = GoogleVisionOcrService.ExtractDocumentNumber(text);

        docNumber.Should().Be("ABC-123456");
    }
}
