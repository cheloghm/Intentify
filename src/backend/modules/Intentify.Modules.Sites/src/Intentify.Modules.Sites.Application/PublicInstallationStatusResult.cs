using Intentify.Modules.Sites.Domain;

namespace Intentify.Modules.Sites.Application;

public sealed record PublicInstallationStatusResult(Site Site, string Origin);
