using System;
using System.IO;
using HushOps.Servers.XiaoHongShu.Services.Notes;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.FileSystem;

/// <summary>
/// 中文：封装 System.IO，提供与 NoteCaptureService 对应的抽象实现，确保路径默认相对于内容根目录解析。
/// </summary>
public sealed class DefaultFileSystem : IFileSystem
{
    private readonly string _contentRoot;

    public DefaultFileSystem(string contentRoot)
    {
        _contentRoot = string.IsNullOrWhiteSpace(contentRoot)
            ? System.IO.Directory.GetCurrentDirectory()
            : contentRoot;

        File = new FileAdapter();
        Directory = new DirectoryAdapter();
        Path = new PathAdapter(_contentRoot);
    }

    public string CurrentDirectory => System.IO.Directory.GetCurrentDirectory();

    public IFile File { get; }

    public IDirectory Directory { get; }

    public IPath Path { get; }

    private sealed class FileAdapter : IFile
    {
        public Stream Create(string path) => System.IO.File.Create(path);
    }

    private sealed class DirectoryAdapter : IDirectory
    {
        public void CreateDirectory(string path) => System.IO.Directory.CreateDirectory(path);

        public bool Exists(string path) => System.IO.Directory.Exists(path);
    }

    private sealed class PathAdapter : IPath
    {
        private readonly string _root;

        public PathAdapter(string root)
        {
            _root = root;
        }

        public string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return _root;
            }

            if (System.IO.Path.IsPathRooted(path))
            {
                return System.IO.Path.GetFullPath(path);
            }

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, path));
        }

        public string Combine(params string[] paths)
        {
            if (paths.Length == 0)
            {
                return _root;
            }

            if (System.IO.Path.IsPathRooted(paths[0]))
            {
                return System.IO.Path.Combine(paths);
            }

            var buffer = new string[paths.Length + 1];
            buffer[0] = _root;
            Array.Copy(paths, 0, buffer, 1, paths.Length);
            return System.IO.Path.Combine(buffer);
        }
    }
}
