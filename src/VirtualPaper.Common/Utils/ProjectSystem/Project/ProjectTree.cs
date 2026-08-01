namespace VirtualPaper.Common.Utils.ProjectSystem.Project {
    /// <summary>
    /// 项目树结构，表示项目中的文件和文件夹层级关系
    /// </summary>
    public class ProjectTree {
        public ProjectFolder Root { get; }

        public ProjectTree(string root) {
            Root = new ProjectFolder {
                Name = Path.GetFileName(root),
                FullPath = root
            };

            Load(Root);
        }

        private void Load(ProjectFolder folder) {
            // 扫描当前层级所有目录
            foreach (var dir in Directory.GetDirectories(folder.FullPath)) {
                var node = new ProjectFolder {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    Parent = folder
                };

                folder.Children.Add(node);
                Load(node);
            }

            // 扫描当前层级所有文件
            foreach (var file in Directory.GetFiles(folder.FullPath)) {
                folder.Children.Add(new ProjectFile {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    LastWriteTime = File.GetLastWriteTime(file),
                    Parent = folder
                });
            }
        }

        public void Add(string path) {
            // 如果父目录不存在于记录中，说明为外部文件，则不添加
            if (Find(Path.GetDirectoryName(path)!) is not ProjectFolder parent)
                return;

            ProjectNode node;
            if (Directory.Exists(path)) {
                node = new ProjectFolder();
            }
            else {
                node = new ProjectFile {
                    LastWriteTime = File.GetLastWriteTime(path)
                };
            }

            node.Name = Path.GetFileName(path);
            node.FullPath = path;
            node.Parent = parent;

            parent.Children.Add(node);
        }

        public void Remove(string path) {
            var node = Find(path);
            node?.Parent?.Children.Remove(node);
        }

        public void Rename(string oldPath, string newPath) {
            var node = Find(oldPath);

            if (node == null)
                return;

            node.FullPath = newPath;
            node.Name = Path.GetFileName(newPath);
        }

        public ProjectNode? Find(string path) {
            return FindNode(Root, path);
        }

        private static ProjectNode? FindNode(ProjectNode node, string path) {
            if (node.FullPath == path)
                return node;

            if (node is ProjectFolder folder) {
                foreach (var child in folder.Children) {
                    var result = FindNode(child, path);

                    if (result != null)
                        return result;
                }
            }

            return null;
        }
    }
}
