using System;
using System.Xml;

class Program
{
    static void Main()
    {
        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load("e:\\wii\\code\\TeknoParrotUI_CN\\TeknoParrotUi\\Properties\\Resources.zh-TW.resx");
            Console.WriteLine("XML file format is correct.");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("XML file format error: " + ex.Message);
            Environment.Exit(1);
        }
    }
}