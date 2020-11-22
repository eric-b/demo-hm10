// Workaround for record/init issue in Visual Studio 2019 16.8.0
// See https://stackoverflow.com/a/62656145/249742

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}
