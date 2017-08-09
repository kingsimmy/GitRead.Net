namespace GitRead.Net.Data
{
    internal enum TreeEntryMode
    {
        Directory = 0b0100_0000_0000_0000, //040000
        RegularNonExecutableFile = 0b1000_0001_1010_0100, //100644
        RegularNonExecutableGroupWriteableFile = 0b1000_0001_1011_0100, //100664
        RegularExecutableFile = 0b1000_0001_1110_1101, //100755
        SymbolicLink = 0b1010_0000_0000_0000, //120000
        Gitlink = 0b1110_0000_0000_0000 //160000
    }
}