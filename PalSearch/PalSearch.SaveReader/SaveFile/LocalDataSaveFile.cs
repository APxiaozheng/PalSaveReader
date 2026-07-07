using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSearch.SaveReader.SaveFile
{
    public class LocalDataSaveFile(IFileSource files) : ISaveFile(files)
    {
    }
}