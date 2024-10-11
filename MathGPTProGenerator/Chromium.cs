using System;
using System.IO;
using System.Net;
using System.Linq;
using OpenQA.Selenium;
using System.Threading;
using SmorcIRL.TempMail;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;
using SmorcIRL.TempMail.Models;
using System.Collections.Generic;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace MathGPTProGenerator
{
    static class Chromium
    {
        public static string version;
        private static ChromeDriver driver;

        public static void Initialization()
        {
            string temp_directory = Path.GetTempPath();
            string current_directory = Directory.GetCurrentDirectory();
            string chromium_directory = Path.Combine(current_directory, "Chromium");
            string driver_path = Path.Combine(current_directory, "chromedriver.exe");

            string driver_endpoint = "https://storage.googleapis.com/chrome-for-testing-public/";
            string github_endpoint = "https://api.github.com/repos/ungoogled-software/ungoogled-chromium-windows/releases";

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.83 Safari/537.36");

                    if (!Directory.Exists(chromium_directory))
                    {
                        Console.WriteLine("Fetching the latest Chromium release from GitHub...");
                        string json = client.DownloadString(github_endpoint);

                        if (!string.IsNullOrEmpty(json))
                        {
                            JArray array = JArray.Parse(json);
                            JObject lasted = (JObject)array.FirstOrDefault();

                            if (lasted != null)
                            {
                                string tag = lasted["tag_name"].ToString();
                                version = Regex.Replace(tag, @"-\d+\.\d+", "");
                                Console.WriteLine($"Latest release: {tag} (driver version: {version})");

                                string file_scheme = Environment.Is64BitOperatingSystem ? "x64.zip" : "x86.zip";
                                IEnumerable<JToken> assets = lasted["assets"].Where(a => a["name"].ToString().Contains(file_scheme));

                                if (assets.Any())
                                {
                                    string chromium_url = assets.First()["browser_download_url"].ToString();
                                    string chromium_zip = Path.Combine(temp_directory, Path.GetFileName(chromium_url));

                                    Console.WriteLine($"Downloading: {Path.GetFileName(chromium_url)}");
                                    client.DownloadFile(chromium_url, chromium_zip);

                                    Console.WriteLine("Extracting Chromium...");
                                    ZipFile.ExtractToDirectory(chromium_zip, current_directory);

                                    string chromium_extracted = Directory.GetDirectories(current_directory).FirstOrDefault(d => d.Contains("ungoogled-chromium"));
                                    if (!string.IsNullOrEmpty(chromium_extracted))
                                    {
                                        Directory.Move(chromium_extracted, chromium_directory);
                                        Console.WriteLine($"Chromium extracted and renamed to: {chromium_directory}");
                                    }

                                    File.Delete(chromium_zip);
                                }
                                else
                                {
                                    Console.WriteLine("No valid file found for download!");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Failed to find the latest release!");
                            }
                        }
                    }
                    else
                    {
                        version = GetChromiumVersion();

                        if (string.IsNullOrEmpty(version))
                        {
                            Console.WriteLine("Could not determine Chromium version. Aborting driver download.");
                            return;
                        }

                        Console.WriteLine("Chromium is already installed. Skipping download.");
                    }

                    if (!File.Exists(driver_path))
                    {
                        if (string.IsNullOrEmpty(version))
                        {
                            Console.WriteLine("Version is not set. Cannot download driver.");
                            return;
                        }

                        string driver_url = $"{driver_endpoint}{version}/win64/chromedriver-win64.zip";
                        string driver_zip = Path.Combine(temp_directory, $"chromedriver_{version}.zip");

                        Console.WriteLine($"Downloading driver for version {version}");
                        client.DownloadFile(driver_url, driver_zip);

                        string driver_extracted = Path.Combine(temp_directory, "chromedriver-temp");
                        Console.WriteLine("Extracting driver...");
                        ZipFile.ExtractToDirectory(driver_zip, driver_extracted);

                        string downloaded_driver_path = Directory.GetFiles(driver_extracted, "chromedriver.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (File.Exists(downloaded_driver_path))
                        {
                            File.Move(downloaded_driver_path, driver_path);
                            Console.WriteLine($"Driver extracted to: {driver_path}");
                        }

                        File.Delete(driver_zip);
                        Directory.Delete(driver_extracted, true);
                    }
                    else
                    {
                        Console.WriteLine("Driver is already installed. Skipping download.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
        }

        public static async Task Start()
        {
            Console.Write("Enter the number of accounts to generate: ");
            int count = int.Parse(Console.ReadLine());

            MailClient client = new MailClient();
            driver = new ChromeDriver(GetChromeDriverService(), GetChromeOptions(), TimeSpan.FromMinutes(2));

            for (int i = 0; i < count; i++)
            {
                driver.Navigate().GoToUrl("https://mathgptpro.com/login/sign-up");
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(45);
                string domain = await client.GetFirstAvailableDomainName();

                string username = Utils.Random(10);
                string password = Utils.Random(16);
                string email = $"{username}@{domain}";

                await client.Register(email, password);

                Input(driver, By.Id("input-email"), email);
                Thread.Sleep(500);
                Click(driver, By.XPath("//*[@id='root']/div/div[1]/div/div/div/div/div[4]/button"));

                MessageInfo[] messages = null;

                while (messages == null || messages.Length == 0)
                {
                    messages = await client.GetAllMessages();
                    await Task.Delay(1000);
                }

                MessageSource source = await client.GetMessageSource(messages[0].Id);

                string url = Utils.ExtractUrlFromHtml(source.Data);

                if (!string.IsNullOrEmpty(url))
                {
                    driver.Navigate().GoToUrl(url);

                    Input(driver, By.Id("input-firstName"), Utils.Random(5));
                    Thread.Sleep(500);
                    Input(driver, By.Id("input-lastName"), Utils.Random(5));
                    Thread.Sleep(500);
                    Input(driver, By.Id("input-password-new"), password);
                    Thread.Sleep(500);
                    Input(driver, By.Id("input-password-again"), password);
                    Thread.Sleep(500);
                    Click(driver, By.XPath("//*[@id='root']/div[1]/div[1]/div/div/div/div/div[7]/button"));
                    Thread.Sleep(500);

                    Console.WriteLine($"{email}:{password}");
                    File.AppendAllText("Accounts.txt", $"{email}:{password}\n");

                    await client.DeleteMessage(messages[0].Id);
                    await client.DeleteAccount();

                    driver.Manage().Cookies.DeleteAllCookies();
                    Thread.Sleep(500);
                }
            }

            driver.Quit();
        }

        private static string GetChromiumVersion()
        {
            string chromium_directory = Path.Combine(Directory.GetCurrentDirectory(), "Chromium");
            string manifest_file = Directory.GetFiles(chromium_directory, "*.manifest").FirstOrDefault();

            if (!string.IsNullOrEmpty(manifest_file))
            {
                string version = Path.GetFileNameWithoutExtension(manifest_file);
                Console.WriteLine($"Chromium version from manifest: {version}");
                return version;
            }

            string chromium_file = Path.Combine(chromium_directory, "chrome.exe");

            if (File.Exists(chromium_file))
            {
                try
                {
                    string version = FileVersionInfo.GetVersionInfo(chromium_file).FileVersion;
                    Console.WriteLine($"Chromium version from chrome.exe: {version}");
                    return version;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get version from chrome.exe: {ex.Message}");
                }
            }

            Console.WriteLine("Version not found!");
            return null;
        }

        private static ChromeDriverService GetChromeDriverService()
        {
            ChromeDriverService service = ChromeDriverService.CreateDefaultService();

            service.EnableVerboseLogging = false;
            service.SuppressInitialDiagnosticInformation = true;

            return service;
        }

        private static ChromeOptions GetChromeOptions()
        {
            ChromeOptions options = new ChromeOptions();
            options.BinaryLocation = Path.Combine(Directory.GetCurrentDirectory(), "Chromium", "chrome.exe");

            options.AddArgument("--start-maximized");
            options.AddExcludedArgument("--enable-automation");
            options.AddUserProfilePreference("credentials_enable_service", false);
            options.AddUserProfilePreference("profile.default_content_setting_values.images", 2);

            //options.AddArgument("--headless");

            return options;
        }

        public static void Click(ChromeDriver driver, By by)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(40));

            bool element = wait.Until(condition =>
            {
                try
                {
                    IWebElement e = driver.FindElement(by);

                    if (e != null && e.Displayed)
                    {
                        e.Click();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
            });
        }

        public static void Input(ChromeDriver driver, By by, string text)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

            bool element = wait.Until(condition =>
            {
                try
                {
                    IWebElement e = driver.FindElements(by).FirstOrDefault();

                    if (e != null && e.Displayed)
                    {
                        e.SendKeys(text);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
            });
        }
    }
}