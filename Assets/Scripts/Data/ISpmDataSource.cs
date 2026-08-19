using System.Collections.Generic;

namespace ShenhuSpm
{
    public interface ISpmDataSource
    {
        List<SpmRecord> Load();
    }
}
