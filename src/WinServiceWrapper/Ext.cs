namespace WinServiceWrapper
{
    using System;
    using System.Linq;

    public static class Ext
    {
        public static bool FirstIs(this string[] args, string target)
        {
            return string.Equals(args.FirstOrDefault(), target, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}