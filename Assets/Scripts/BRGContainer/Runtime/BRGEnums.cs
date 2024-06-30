namespace BRGContainer.Runtime
{
    public enum EBRGViewType : int
    {
        Forward = 0,
        Shadow0 = 1,
        Shadow1 = 2,
        Shadow2 = 3,
        ViewCount = 4
    }

    public enum EGetNextActiveIndexInfo : int
    {
        None = 0,
        NeedResize = 1,
        NeedExtentInstanceCount = 2,
    }
}