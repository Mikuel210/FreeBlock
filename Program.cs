using System.IO;

// Test writing to hosts file
using StreamWriter file = new("/etc/hosts");
file.WriteLine("0.0.0.0 youtube.com");

Console.WriteLine("YouTube is now blocked!");
