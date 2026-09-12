using Playnite.SDK;

namespace PlayniteDlssgPlugin
{
    internal static class Loc
    {
        public static string Get(string key)
        {
            return ResourceProvider.GetResource(key) as string ?? key;
        }
    }
}
