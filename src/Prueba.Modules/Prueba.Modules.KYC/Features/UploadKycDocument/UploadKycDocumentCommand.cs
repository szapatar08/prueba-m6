namespace Prueba.Modules.KYC.Features.UploadKycDocument;

public record UploadKycDocumentCommand(
    string FileName,
    string ContentType,
    Stream DocumentStream);
