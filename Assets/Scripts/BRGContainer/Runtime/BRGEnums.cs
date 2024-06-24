namespace BRGContainer.Runtime
{
    public enum BRGViewType : int
    {
        EForward = 0,
        EShadow0 = 1,
        EShadow1 = 2,
        EShadow2 = 3,
        EViewCount = 4
    }

    public enum EGetNextAliveIndexInfo : int
    {
        None = 0,
        NeedResize = 1,
        NeedExtentInstanceCount = 2,
    }
}