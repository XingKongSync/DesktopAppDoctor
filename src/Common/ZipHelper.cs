using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public static class ZipHelper
    {

        public static bool ZipDirectoryWithExtraFile(string folderToZip, string zipedFile, CancellationTokenSource cancellationTokenSource = null, string extraFile = null)
        {
            bool result = false;
            if (!Directory.Exists(folderToZip)) { return result; }

            cancellationTokenSource?.Token.ThrowIfCancellationRequested();

            ZipOutputStream zipStream = new ZipOutputStream(File.Create(zipedFile));
            using (zipStream)
            {
                cancellationTokenSource?.Token.ThrowIfCancellationRequested();

                zipStream.SetLevel(6);
                result = ZipDirectory(folderToZip, zipStream, "", cancellationTokenSource);
                if (!string.IsNullOrEmpty(extraFile))
                    result &= ZipFile(extraFile, zipStream, cancellationTokenSource);

                zipStream.Finish();
                zipStream.Close();
            }
            return result;
        }

        private static bool ZipFile(string fileToZip, ZipOutputStream zipStream, CancellationTokenSource cancellationTokenSource = null)
        {
            FileInfo fi = new FileInfo(fileToZip);
            if (!fi.Exists) { return false; }

            FileStream fs = null;
            try
            {
                SpinWait.SpinUntil(() => TryGetFileStream(fi, out fs));
                if (fs == null)
                    return false;
                using (fs)
                {
                    ZipEntry ent = new ZipEntry(Path.GetFileName(fileToZip));
                    ent.DateTime = fi.LastWriteTime;
                    ent.Size = fi.Length;

                    zipStream.PutNextEntry(ent);
                    zipStream.SetLevel(6);

                    if (cancellationTokenSource == null)
                    {
                        fs.CopyTo(zipStream);
                    }
                    else
                    {
                        fs.CopyToAsync(zipStream, 81920, cancellationTokenSource.Token).Wait();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (null != fs)
                    fs.Dispose();
            }
        }

        private static bool TryGetFileStream(FileInfo info, out FileStream fs)
        {
            try { info.Refresh(); } catch { }

            try
            {
                fs = info.OpenRead();
                return true;
            }
            catch (IOException)
            {
                fs = null;
                return false;
            }
            catch (Exception)
            {
                fs = null;
                return true;
            }
        }

        /// <summary>
        /// 递归压缩文件夹的内部方法
        /// </summary>
        /// <param name="folderToZip">要压缩的文件夹路径</param>
        /// <param name="zipStream">压缩输出流</param>
        /// <param name="parentFolderName">此文件夹的上级文件夹</param>
        /// <returns></returns>
        private static bool ZipDirectory(string folderToZip, ZipOutputStream zipStream, string parentFolderName, CancellationTokenSource cancellationTokenSource = null)
        {
            bool result = true;
            string[] folders, files;
            ZipEntry ent = null;
            FileStream fs = null;
            //Crc32 crc = new Crc32();

            try
            {
                //string entName = string.Empty;/*folderToZip.Replace(mRootPath, string.Empty) + "/";*/
                //Path.Combine(parentFolderName, Path.GetFileName(folderToZip) + "/")
                //ent = new ZipEntry(entName);
                //zipStream.PutNextEntry(ent);
                //zipStream.Flush();

                files = Directory.GetFiles(folderToZip);
                foreach (string file in files)
                {
                    cancellationTokenSource?.Token.ThrowIfCancellationRequested();

                    fs = File.OpenRead(file);
                    using (fs)
                    {

                        ent = new ZipEntry(Path.Combine(parentFolderName, Path.GetFileName(file)));
                        ent.DateTime = DateTime.Now;
                        ent.Size = fs.Length;

                        zipStream.PutNextEntry(ent);
                        if (cancellationTokenSource == null)
                        {
                            fs.CopyTo(zipStream);
                        }
                        else
                        {
                            fs.CopyToAsync(zipStream, 81920, cancellationTokenSource.Token).Wait();
                        }
                    }
                    fs = null;
                }

            }
            catch
            {
                result = false;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
                if (ent != null)
                {
                    ent = null;
                }
                GC.Collect();
                GC.Collect(1);
            }

            folders = Directory.GetDirectories(folderToZip);
            foreach (string folder in folders)
                if (!ZipDirectory(folder, zipStream, Path.GetFileName(folder), cancellationTokenSource))
                    return false;

            return result;
        }


    }
}
