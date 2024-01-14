using System;
using System.IO;
using Microsoft.Win32;

class Program
{
    static void Main()
    {
        Console.WriteLine("在运行本程序之前，您需要确保本程序是处于管理员模式运行的，否则会运行失败");
        Console.WriteLine("请输入有效的 exe 文件路径:");
        string exePath = Console.ReadLine();

        if (File.Exists(exePath) && Path.GetExtension(exePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            AddPathEnvironmentVariable(exePath);
            Console.WriteLine("路径已成功添加到系统 PATH 环境变量中。");
        }
        else
        {
            Console.WriteLine("输入的路径无效或不是一个有效的 exe 文件。");
        }

        Console.ReadLine();
    }

    static void AddPathEnvironmentVariable(string exePath)
    {
        string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
        string newPath = currentPath + ";" + Path.GetDirectoryName(exePath);

        // 更新系统 PATH 环境变量
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);

        // 如果你希望当前窗口中立即生效，可以使用下面的语句：
        // Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
    }
}
