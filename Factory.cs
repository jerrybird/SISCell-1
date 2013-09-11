using System;
using System.Reflection;

namespace SISCell
{
    interface iProtocol
    {
        bool Connected { get; }
        bool Connect();
        void DisConnect();
        void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst);
        bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst);
    }

    interface oProtocol
    {
        bool Connected { get; }
        bool Connect();
        void DisConnect();
        void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst);
        void SetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst);
    }

    class Factory
    {
        public iProtocol MakeInput(string className)
        {
            iProtocol MyProtocol = null;

            Type type = Type.GetType(MethodBase.GetCurrentMethod().DeclaringType.Namespace + ".I_" + className, true);
            MyProtocol = (iProtocol)Activator.CreateInstance(type);

            return MyProtocol;
        }

        public oProtocol MakeOutput(string className)
        {
            oProtocol MyProtocol = null;

            Type type = Type.GetType(MethodBase.GetCurrentMethod().DeclaringType.Namespace + ".O_" + className, true);
            MyProtocol = (oProtocol)Activator.CreateInstance(type);

            return MyProtocol;
        }
    }
}
