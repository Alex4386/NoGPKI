﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;

namespace NoGPKI
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public X509Store lmstore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        public X509Store lmauthrootstore = new X509Store(StoreName.AuthRoot, StoreLocation.LocalMachine);
        public X509Store custore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        public X509Store cuauthrootstore = new X509Store(StoreName.AuthRoot, StoreLocation.CurrentUser);
        public X509Certificate2 gpki = new X509Certificate2();
        public string gpkiMD5 = "fa396a2bb384a05f3fa237609d68d516";
        public string gpkiPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"assets\\gpkiroot.cer");
        public X509Certificate2 gpkiCert;

        public MainWindow()
        {
            try
            {
                lmstore.Open(OpenFlags.ReadWrite);
                custore.Open(OpenFlags.ReadWrite);
                lmauthrootstore.Open(OpenFlags.ReadWrite);
                cuauthrootstore.Open(OpenFlags.ReadWrite);
            }
            catch (CryptographicException e) {
                MessageBox.Show("로컬 컴퓨터의 CA Root 인증서 RW 마운트 실패", "오류: 권한 부족", MessageBoxButton.OK, MessageBoxImage.Error);   
                this.Close();
            }

            if (!VerifyIsGPKI())
            {
                GPKICheckSumFail();
                this.Close();
            } else {
                gpkiCert = new X509Certificate2(gpkiPath);
            }

            InitializeComponent();
            var certs = lmstore.Certificates.Find(X509FindType.FindByThumbprint, gpkiCert.Thumbprint, false);
            var acerts = lmauthrootstore.Certificates.Find(X509FindType.FindByThumbprint, gpkiCert.Thumbprint, false);
            var cu_certs = custore.Certificates.Find(X509FindType.FindByThumbprint, gpkiCert.Thumbprint, false);
            var acu_certs = cuauthrootstore.Certificates.Find(X509FindType.FindByThumbprint, gpkiCert.Thumbprint, false);

            if ((certs != null && certs.Count > 0) || (acerts != null && acerts.Count > 0))
            {
                if (certs[0].PrivateKey == gpkiCert.PrivateKey)
                {
                    certStat.Content = "비밀키가 일치하는 GPKI인증서 발견됨";
                    statusLabel.Content = "준비 완료! 명령만 주세요!";
                } else
                {
                    certStat.Content = "지문만 일치하는 GPKI인증서 발견됨";
                    statusLabel.Content = "뭔가 이상해요! README를 읽어봐요!";
                }
                statusLabel.Content += (acerts != null && acerts.Count > 0) ? "(LM+)":" (LM)";
            } else
            {
                certStat.Content = "GPKI인증서가 발견되지 않음.";
                statusLabel.Content = "준비 완료! 명령만 주세요!";
            }

            if ((cu_certs != null && cu_certs.Count > 0) || (acu_certs != null && acerts.Count > 0))
            {
                statusLabel.Content += (acu_certs != null && acerts.Count > 0) ? " (CU+)":" (CU)";
            }


        }

        public void GPKICheckSumFail()
        {
            MessageBox.Show("MD5 체크섬 검사 실패", "오류: 유효성 검사 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            MessageBox.Show("실행 경로아래에 assets\\gpkiroot.cer 파일이 있는지 확인 해 주시기 바랍니다.\n있다면, 파일이 손상된것 같습니다.", "오류: 유효성 검사 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            
        }

        public bool VerifyIsGPKI()
        {
            MD5 md5Hash = MD5.Create();
            return VerifyMd5Hash(md5Hash, gpkiPath, gpkiMD5);
        }

        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            //byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            byte[] data = md5Hash.ComputeHash(File.ReadAllBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        // Verify a hash against a string.
        static bool VerifyMd5Hash(MD5 md5Hash, string input, string hash)
        {
            // Hash the input.
            string hashOfInput = GetMd5Hash(md5Hash, input);

            // Create a StringComparer an compare the hashes.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            /*
            if (0 == comparer.Compare(hashOfInput, hash))
            {
                return true;
            }
            else
            {
                return false;
            }
            */
            return (0 == comparer.Compare(hashOfInput, hash));
        }

        private void deleteCert_Click(object sender, RoutedEventArgs e)
        {
            statusProgress.Value = 0;
            statusLabel.Content = "GPKI 인증서 삭제 확인";
            MessageBoxResult i = MessageBox.Show("정말로 인증서를 삭제하시겠습니까?", "인증서 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
            statusProgress.Value = 50;
            if (i == MessageBoxResult.Yes)
            {
                statusLabel.Content = "GPKI 인증서 체크섬 검사 중";
                if (!VerifyIsGPKI())
                {
                    statusLabel.Content = "GPKI 인증서 체크섬 검사 실패!";
                    GPKICheckSumFail();
                    return;
                }
                statusLabel.Content = "GPKI 인증서 삭제 시작";
                try
                {
                    statusLabel.Content = "LM 에서 인증서 삭제중";
                    lmstore.Remove(gpkiCert);
                    statusProgress.Value = 60;
                    statusLabel.Content = "LM AuthRoot 에서 인증서 삭제중";
                    lmauthrootstore.Remove(gpkiCert);
                    statusProgress.Value = 70;
                    statusLabel.Content = "CU 에서 인증서 삭제중";
                    custore.Remove(gpkiCert);
                    statusProgress.Value = 80;
                    statusLabel.Content = "CU AuthRoot 에서 인증서 삭제중";
                    cuauthrootstore.Remove(gpkiCert);
                    statusProgress.Value = 90;
                    statusLabel.Content = "GPKI 인증서 삭제 완료";
                    statusProgress.Value = 100;
                } catch
                {
                    statusLabel.Content = "GPKI 인증서 처리중 예외 발생!";
                }
            } else
            {
                statusProgress.Value = 0;
                statusLabel.Content = "GPKI 인증서 삭제 취소";
            }
        }

        private void recoverCert_Click(object sender, RoutedEventArgs e)
        {
            statusProgress.Value = 0;
            statusLabel.Content = "GPKI 인증서 복구 확인";
            MessageBoxResult i = MessageBox.Show("정말로 인증서를 추가하시겠습니까?", "인증서 추가", MessageBoxButton.YesNo, MessageBoxImage.Question);
            statusProgress.Value = 50;
            if (i == MessageBoxResult.Yes)
            {
                statusLabel.Content = "GPKI 인증서 체크섬 검사 중";
                if (!VerifyIsGPKI())
                {
                    statusLabel.Content = "GPKI 인증서 체크섬 검사 실패!";
                    GPKICheckSumFail();
                    return;
                }
                try
                {
                    statusLabel.Content = "GPKI 인증서 복구 시작";
                    statusLabel.Content = "LM 에 인증서 추가 중";
                    lmstore.Add(gpkiCert);
                    statusProgress.Value = 60;
                    statusLabel.Content = "LM AuthRoot 에 인증서 추가중";
                    lmauthrootstore.Add(gpkiCert);
                    statusProgress.Value = 70;
                    statusLabel.Content = "CU 에 인증서 추가중";
                    custore.Add(gpkiCert);
                    statusProgress.Value = 80;
                    statusLabel.Content = "CU AuthRoot 에 인증서 추가중";
                    cuauthrootstore.Add(gpkiCert);
                    statusProgress.Value = 90;
                } catch
                {

                }
                statusLabel.Content = "GPKI 인증서 복구 완료";
                statusProgress.Value = 100;
            } else
            {
                statusProgress.Value = 0;
                statusLabel.Content = "GPKI 인증서 복구 취소";
            }
        }
    }
}
