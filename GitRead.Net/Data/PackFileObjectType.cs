namespace GitRead.Net.Data
{
    public enum PackFileObjectType : byte
    {
        Commit = 0b0000_0001,
        Tree = 0b0000_0010,
        Blob = 0b0000_0011,
        Tag = 0b0000_0100,
        DeltaOffsetToBase = 0b0000_0110, //DELTA_ENCODED object w/ offset to base
        DeltaOffsetToObj = 0b0000_0111 //DELTA_ENCODED object w/ base BINARY_OBJ_ID */
    }
}