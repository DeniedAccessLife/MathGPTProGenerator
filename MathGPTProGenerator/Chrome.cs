using System;
using System.IO;
using System.Net;
using System.Linq;
using Microsoft.Win32;
using OpenQA.Selenium;
using System.Threading;
using SmorcIRL.TempMail;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;
using SmorcIRL.TempMail.Models;
using System.Security.Principal;
using OpenQA.Selenium.Support.UI;
using System.Security.AccessControl;
using System.Collections.ObjectModel;

namespace MathGPTProGenerator
{
    static class Chrome
    {
        public static string version;
        private static string updater;
        private static ChromeDriver driver;

        public static async Task Initialization()
        {
            string uninstallKeyPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome";
            string updateKeyPath = @"SOFTWARE\WOW6432Node\Google\Update";

            using (RegistryKey uninstall = Registry.LocalMachine.OpenSubKey(uninstallKeyPath))
            {
                if (uninstall == null)
                {
                    Installing();
                }
            }

            while (Registry.LocalMachine.OpenSubKey(uninstallKeyPath) == null || Registry.LocalMachine.OpenSubKey(updateKeyPath) == null)
            {
                await Task.Delay(1000);
            }

            using (RegistryKey uninstall = Registry.LocalMachine.OpenSubKey(uninstallKeyPath))
            {
                if (uninstall != null)
                {
                    version = uninstall.GetValue("Version").ToString();
                }
            }

            using (RegistryKey update = Registry.LocalMachine.OpenSubKey(updateKeyPath))
            {
                if (update != null)
                {
                    updater = update.GetValue("Path").ToString();
                }
            }
        }

        public static void CheckUpdateStatus()
        {
            Utils.CheckProcesses();
            Console.WriteLine("Checking status of auto-updates...");

            if (updater != null && File.Exists(updater))
            {
                FileSecurity security = File.GetAccessControl(updater);
                AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

                if (rules.Count > 0)
                {
                    Console.WriteLine("Disabling chrome auto-updates...");

                    foreach (FileSystemAccessRule rule in rules)
                    {
                        security.RemoveAccessRule(rule);
                    }

                    security.SetAccessRuleProtection(true, false);
                    File.SetAccessControl(updater, security);
                }
            }
            else
            {
                Console.WriteLine("Updater file does not exist!");
            }
        }

        public static void Installing()
        {
            Console.WriteLine("Perform chrome installation...");

            using (WebClient client = new WebClient())
            {
                string[] files = { "ChromeSetup.exe", "ChromeDriver.exe" };
                string url = "https://github.com/DeniedAccessLife/MathGPTProGenerator/raw/main/MathGPTProGenerator/MathGPTProGenerator/bin/Debug/";

                foreach (string file in files)
                {
                    string localPath = Path.Combine(Directory.GetCurrentDirectory(), file);

                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                    client.DownloadFile(url + file, localPath);
                }
            }

            string installer = Path.Combine(Directory.GetCurrentDirectory(), "ChromeSetup.exe");

            Process process = new Process();
            process.StartInfo.FileName = installer;
            process.StartInfo.Arguments = "/silent /install";
            process.Start();

            if (!process.WaitForExit(120000))
            {
                Utils.KillProcessChildren(process.Id);
                Console.WriteLine("The installation process did not complete within the expected time!");
                Console.WriteLine("To continue working, try to reboot the system, or start the installation process yourself.");

                Console.ReadKey();
                Environment.Exit(1);
            }

            process.WaitForExit();

            try
            {
                File.Delete(installer);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Failed to delete file due to access restrictions: {ex.Message}");
            }
        }

        public static async Task Start()
        {
            CheckUpdateStatus();

            Console.Write("Enter the number of accounts to generate: ");
            int count = int.Parse(Console.ReadLine());

            MailClient client = new MailClient();
            driver = new ChromeDriver(GetChromeDriverService(), GetChromeOptions(), TimeSpan.FromMinutes(2));

            for (int i = 0; i < count; i++)
            {
                driver.Navigate().GoToUrl("https://mathgptpro.com/new");
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

                    Click(driver, By.CssSelector(".MuiAutocomplete-popupIndicator"));

                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                    wait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.CssSelector(".MuiAutocomplete-option")));

                    ReadOnlyCollection<IWebElement> options = driver.FindElements(By.CssSelector(".MuiAutocomplete-option"));

                    Random random = new Random();
                    int index = random.Next(options.Count);
                    options[index].Click();

                    Input(driver, By.Id("input-password-new"), password);
                    Thread.Sleep(500);
                    Input(driver, By.Id("input-password-again"), password);
                    Thread.Sleep(500);
                    Click(driver, By.XPath("//*[@id='root']/div/div[1]/div/div/div/div/div[8]/button"));
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