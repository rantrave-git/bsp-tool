// See https://aka.ms/new-console-template for more information
using System;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using AwesomeSockets;
using Silk.NET.Core.Native;
using System.IO;

unsafe
{
    using VkApplication app = new VkApplication("Test app", "no engine");
    using var uniformBuffer = app.CreateBuffer(1024, BufferUsageFlags.UniformBufferBit | BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.DeviceLocalBit);

    using var shader = app.CreateShader(File.ReadAllBytes("shader.spv"));
    // Span<DescriptorSetLayoutBinding> layoutBindings = stackalloc DescriptorSetLayoutBinding[3];
    // layoutBindings[0] = new DescriptorSetLayoutBinding
    // {
    //     Binding = 0,
    //     DescriptorCount = 1,
    //     DescriptorType = DescriptorType.UniformBuffer,
    //     PImmutableSamplers = (Sampler*)IntPtr.Zero,
    //     StageFlags = ShaderStageFlags.ComputeBit,
    // };
    // layoutBindings[1] = new DescriptorSetLayoutBinding
    // {
    //     Binding = 1,
    //     DescriptorCount = 1,
    //     DescriptorType = DescriptorType.StorageBuffer,
    //     PImmutableSamplers = (Sampler*)IntPtr.Zero,
    //     StageFlags = ShaderStageFlags.ComputeBit,
    // };
    // layoutBindings[2] = new DescriptorSetLayoutBinding
    // {
    //     Binding = 2,
    //     DescriptorCount = 1,
    //     DescriptorType = DescriptorType.StorageBuffer,
    //     PImmutableSamplers = (Sampler*)IntPtr.Zero,
    //     StageFlags = ShaderStageFlags.ComputeBit,
    // };
    // DescriptorSetLayout computeDescriptorSetLayout;
    // PipelineLayout pipelineLayout;
    // fixed (DescriptorSetLayoutBinding* layout = layoutBindings)
    // {
    //     var desclayout = new DescriptorSetLayoutCreateInfo()
    //     {
    //         SType = StructureType.DescriptorSetLayoutCreateInfo,
    //         BindingCount = 3,
    //         PBindings = layout
    //     };
    //     res = vk.CreateDescriptorSetLayout(logicalDevice, in desclayout, (AllocationCallbacks*)IntPtr.Zero, out computeDescriptorSetLayout);
    //     Console.WriteLine(res);
    // }
    // PipelineLayoutCreateInfo layoutCreateInfo = new PipelineLayoutCreateInfo()
    // {
    //     SType = StructureType.PipelineLayoutCreateInfo,
    //     SetLayoutCount = 1,
    //     PSetLayouts = &computeDescriptorSetLayout
    // };

    // res = vk.CreatePipelineLayout(logicalDevice, in layoutCreateInfo, (AllocationCallbacks*)IntPtr.Zero, out pipelineLayout);
    // Console.WriteLine(res);



    // Console.WriteLine(cnt[0].ToString() + devices.ToString());

    // // memHandle.Dispose();
    // // Marshal.FreeHGlobal((IntPtr)appinfo.PApplicationName);
    // vk.DestroyDescriptorSetLayout(logicalDevice, computeDescriptorSetLayout, (AllocationCallbacks*)IntPtr.Zero);
}

// using Vulkan;
// using static Vulkan.Vk;
// using static Vulkan.Utils;

// VkInstance inst;

// using (var ai = new VkApplicationInfo(
//     new Vulkan.Version(1, 2, 0),
//     new Vulkan.Version(1, 2, 0),
//     new Vulkan.Version(1, 3, 0)))
// using (PinnedObjects po = new PinnedObjects())
// {
//     IntPtr[] instanceExts = { Ext.I.VK_KHR_get_physical_device_properties2.Pin(po) };
//     IntPtr[] layers = { "VK_LAYER_KHRONOS_validation".Pin(po) };
//     using (VkInstanceCreateInfo ci = new VkInstanceCreateInfo
//     {
//         pApplicationInfo = ai,
//         enabledExtensionCount = 1,
//         enabledLayerCount = 1,
//         ppEnabledExtensionNames = instanceExts.Pin(po),
//         ppEnabledLayerNames = layers.Pin(po)
//     })
//     {
//         CheckResult(vkCreateInstance(ci, IntPtr.Zero, out inst));
//     }
// }
if (false)
{
    var s = AwesomeSockets.Sockets.AweSock.TcpListen(2233);
    // using var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    // s.Bind(new IPEndPoint(IPAddress.Any, 2233));
    // s.Listen(100);
    // using var cl = s.Accept();
    var cl = s.Accept();
    // Span<byte> buffer = stackalloc byte[1024];
    var buffer = AwesomeSockets.Buffers.Buffer.New(1024);
    // buffer[0] = 100;
    // buffer[1] = 102;
    // buffer[2] = 0;
    var (len, _) = cl.ReceiveMessage(buffer);
    Span<char> chars = stackalloc char[1024];

    var dec = Encoding.UTF8.GetDecoder();
    AwesomeSockets.Buffers.Buffer.FinalizeBuffer(buffer);
    dec.Convert(AwesomeSockets.Buffers.Buffer.GetBuffer(buffer), chars, true, out var bytenum, out var chrnum, out var complete);

    Console.WriteLine(chars[..chrnum].ToString());
    // AwesomeSockets.Buffers.Buffer.ClearBuffer(buffer);
    // Console.WriteLine("asd");
    cl.SendMessage(buffer);

}
Console.WriteLine("Hallo");