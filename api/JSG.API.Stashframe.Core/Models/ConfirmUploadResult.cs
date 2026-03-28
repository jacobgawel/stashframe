using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Models;

public record ConfirmUploadResult(ConfirmUploadStatus Status, MediaCategory? Category = null);