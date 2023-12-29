// using System;
// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;
// using Vulkan;
// using static Vulkan.Vk;


// namespace TestVk;
// public static class Utils
// {
//     public static void CheckResult(VkResult result, string msg = "Call failed",
//         [CallerMemberName] string caller = null,
//         [CallerFilePath] string sourceFilePath = "",
//         [CallerLineNumber] int sourceLineNumber = 0)
//     {
//         if (result != VkResult.Success)
//             throw new InvalidOperationException($"[{sourceFilePath}:{sourceLineNumber}->{caller}]{msg}: {result}");
//     }
//     public static bool TryGetPhysicalDevice(VkInstance inst, VkPhysicalDeviceType deviceType, out VkPhysicalDevice phy)
//     {
//         CheckResult(vkEnumeratePhysicalDevices(inst, out uint phyCount, IntPtr.Zero));

//         VkPhysicalDevice[] phys = new VkPhysicalDevice[phyCount];

//         CheckResult(vkEnumeratePhysicalDevices(inst, out phyCount, phys.Pin()));

//         for (int i = 0; i < phys.Length; i++)
//         {
//             phy = phys[i];
//             vkGetPhysicalDeviceProperties(phy, out VkPhysicalDeviceProperties props);
//             if (props.deviceType == deviceType)
//                 return true;
//         }
//         phy = default;
//         return false;
//     }
//     public static VkPhysicalDeviceToolProperties[] GetToolProperties(VkPhysicalDevice phy)
//     {
//         CheckResult(vkGetPhysicalDeviceToolProperties(phy, out uint count, IntPtr.Zero));
//         int sizeStruct = Marshal.SizeOf<VkPhysicalDeviceToolProperties>();
//         IntPtr ptrTools = Marshal.AllocHGlobal(sizeStruct * (int)count);
//         CheckResult(vkGetPhysicalDeviceToolProperties(phy, out count, ptrTools));

//         VkPhysicalDeviceToolProperties[] result = new VkPhysicalDeviceToolProperties[count];
//         IntPtr tmp = ptrTools;
//         for (int i = 0; i < count; i++)
//         {
//             result[i] = Marshal.PtrToStructure<VkPhysicalDeviceToolProperties>(tmp);
//             tmp += sizeStruct;
//         }

//         Marshal.FreeHGlobal(ptrTools);
//         return result;
//     }
// }