using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Ageisynth_CybersecurityBot_WPF
{
    public partial class MainWindow : Window
    {
        private readonly AgeisynthBotInterface bot;

        public MainWindow()
        {
            InitializeComponent();
            bot = new AgeisynthBotInterface(ChatPanel, UserInputTextBox, TaskListView, QuizPanel, DisplayedQuestion,
                FirstChoiceButton, SecondChoiceButton, ThirdChoiceButton, FourthChoiceButton, SubmitAnswerButton, DisplayScore);
            bot.PlayWelcomeMessage();
            bot.GetUserName();
            bot.CheckReminders();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessUserInput();
        }

        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessUserInput();
                e.Handled = true;
            }
        }

        private void TaskListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TaskListView.SelectedItem is Task selectedTask)
            {
                bot.ToggleTaskStatus(selectedTask);
            }
        }

        private void ClearCompletedButton_Click(object sender, RoutedEventArgs e)
        {
            bot.ClearCompletedTasks();
        }

        private void HandleAnswerSelection(object sender, RoutedEventArgs e)
        {
            bot.HandleAnswerSelection(sender as Button);
        }

        private void HandleNextQuestion(object sender, RoutedEventArgs e)
        {
            bot.HandleNextQuestion();
        }

        private void ProcessUserInput()
        {
            string userInput = UserInputTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(userInput))
            {
                bot.ProcessUserInput(userInput);
                UserInputTextBox.Clear();
            }
        }
    }

    public class Task
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? ReminderDate { get; set; }
        public string Status { get; set; } = "Pending";

        public override string ToString()
        {
            return $"{Title} - {Description} - {(ReminderDate.HasValue ? ReminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "No Reminder")} - {Status}";
        }
    }

    public class QuizQuestion
    {
        public string Question { get; set; }
        public List<string> Choices { get; set; }
        public string CorrectChoice { get; set; }
        public string Explanation { get; set; }
    }

    public class VoiceGreeting
    {
        public VoiceGreeting()
        {
            PlayWelcomeMessage();
        }

        private void PlayWelcomeMessage()
        {
            try
            {
                string fullLocation = AppDomain.CurrentDomain.BaseDirectory;
                string newPath = fullLocation.Replace("bin\\Debug\\net8.0-windows", "");
                string fullPath = Path.Combine(newPath, "greeting.wav");
                MediaPlayer player = new MediaPlayer();
                
                    player.Open(new Uri(fullPath));
                    player.Play();
                
            }
            catch (Exception audioError)
            {
                MessageBox.Show($"Error playing welcome message: {audioError.Message}");
            }
        }
    }

    public class AgeisynthBotInterface
    {
        private string userName = string.Empty;
        private Dictionary<string, string> userMemory = new Dictionary<string, string>();
        private const string MEMORY_FILE = "memory.txt";
        private string currentTopic = string.Empty;
        private List<string> conversationHistory = new List<string>();
        private List<string> recentActions = new List<string>(); // Activity log
        private string pendingFollowUp = string.Empty;
        private Dictionary<string, Dictionary<string, string>> followUpResponses = new Dictionary<string, Dictionary<string, string>>();
        private Dictionary<string, bool> detectedSentiments = new Dictionary<string, bool>
        {
            { "worried", false }, { "confused", false }, { "frustrated", false }, { "curious", false }, { "happy", false }
        };
        private HashSet<string> ignoreWords = new HashSet<string>();
        private Dictionary<string, List<string>> topicResponses = new Dictionary<string, List<string>>();
        private Dictionary<string, string> specialQuestions = new Dictionary<string, string>();
        private List<string> defaultResponses = new List<string>
        {
            "I didn't quite understand that. Could you rephrase?",
            "I'm not sure I follow. Can you try asking that in a different way?",
            "I'm still learning! Could you phrase that differently?",
            "I'm not familiar with that topic yet. Would you like to know about password safety, phishing, or malware instead?"
        };
        private readonly StackPanel chatPanel;
        private readonly TextBox userInputTextBox;
        private readonly ListView taskListView;
        private readonly Border quizPanel;
        private readonly TextBlock displayedQuestion;
        private readonly Button firstChoiceButton;
        private readonly Button secondChoiceButton;
        private readonly Button thirdChoiceButton;
        private readonly Button fourthChoiceButton;
        private readonly Button submitAnswerButton;
        private readonly TextBlock displayScore;
        private List<Task> tasks = new List<Task>();
        private List<QuizQuestion> quizData = new List<QuizQuestion>();
        private const string TASKS_FILE = "tasks.txt";
        private int questionIndex = 0;
        private int currentScore = 0;
        private Button selectedChoice = null;
        private Button correctChoiceButton = null;
        private bool quizMode = false;

        // ML.NET sentiment analysis
        private readonly MLContext mlContext;
        private List<SentimentData> trainingData;
        private PredictionEngine<SentimentData, SentimentPrediction> predEngine;
        private string userInput;

        private class SentimentData
        {
            public string Text { get; set; }
            public bool Label { get; set; }
        }

        private class SentimentPrediction
        {
            [ColumnName("PredictedLabel")]
            public bool Prediction { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }
        }

        public AgeisynthBotInterface(StackPanel chatPanel, TextBox userInputTextBox, ListView taskListView, Border quizPanel,
            TextBlock displayedQuestion, Button firstChoiceButton, Button secondChoiceButton, Button thirdChoiceButton,
            Button fourthChoiceButton, Button submitAnswerButton, TextBlock displayScore)
        {
            this.chatPanel = chatPanel;
            this.userInputTextBox = userInputTextBox;
            this.taskListView = taskListView;
            this.quizPanel = quizPanel;
            this.displayedQuestion = displayedQuestion;
            this.firstChoiceButton = firstChoiceButton;
            this.secondChoiceButton = secondChoiceButton;
            this.thirdChoiceButton = thirdChoiceButton;
            this.fourthChoiceButton = fourthChoiceButton;
            this.submitAnswerButton = submitAnswerButton;
            this.displayScore = displayScore;
            InitializeIgnoreWords();
            InitializeResponses();
            InitializeSpecialQuestions();
            InitializeFollowUpResponses();
            LoadMemory();
            LoadTasks();
            LoadQuizData();

            // Initialize ML.NET for sentiment analysis
            mlContext = new MLContext();
            trainingData = new List<SentimentData>
            {
                new SentimentData { Text = "I am happy", Label = true },
                new SentimentData { Text = "I hate this", Label = false },
                new SentimentData { Text = "I am sad", Label = false },
                new SentimentData { Text = "I am good", Label = true },
                new SentimentData { Text = "I'm feeling great", Label = true },
                new SentimentData { Text = "This is frustrating", Label = false },
                new SentimentData { Text = "I'm worried about security", Label = false },
                new SentimentData { Text = "I'm curious about phishing", Label = true }
            };
            TrainSentimentModel();

            // Ensure SubmitAnswerButton is enabled
            submitAnswerButton.IsEnabled = true;
            submitAnswerButton.Content = "Submit Answer";
        }

        private void TrainSentimentModel()
        {
            try
            {
                var trainDataView = mlContext.Data.LoadFromEnumerable(trainingData);
                var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(SentimentData.Text))
                    .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label", featureColumnName: "Features"));
                var model = pipeline.Fit(trainDataView);
                predEngine = mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(model);
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"Error training sentiment model: {ex.Message}");
            }
        }

        public void PlayWelcomeMessage()
        {
            new VoiceGreeting();
        }

        public void GetUserName()
        {
            if (userMemory.TryGetValue("name", out string storedName))
            {
                DisplayBotMessage($"Welcome back, {storedName}! Is that still you? (yes/no)");
                userInputTextBox.Tag = "confirmName";
            }
            else
            {
                DisplayBotMessage("Please enter your name.");
                userInputTextBox.Tag = "newName";
            }
        }

        public void ProcessUserInput(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                DisplayBotMessage(GetRandomResponse(defaultResponses));
                return;
            }

            // Display user input in chat
            TextBlock userMessage = new TextBlock
            {
                Text = $"You: {userInput}",
                Foreground = Brushes.Cyan,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            };
            chatPanel.Children.Add(userMessage);

            // Perform sentiment analysis
            var sentiment = AnalyzeSentiment(userInput);
            string sentimentResponse = GetSentimentResponse(sentiment);
            if (!string.IsNullOrEmpty(sentimentResponse))
            {
                DisplayBotMessage(sentimentResponse);
            }

            // Handle name input
            if (userInputTextBox.Tag?.ToString() == "confirmName")
            {
                if (userInput.ToLower() == "yes" || userInput.ToLower() == "y")
                {
                    userName = userMemory["name"];
                    userInputTextBox.Tag = null;
                    DisplayBotMessage($"Hey {userName}, can I assist you with cybersecurity questions, manage tasks, or start a quiz? Try commands like 'add task Enable 2FA' or 'start quiz'.");
                    if (userMemory.TryGetValue("interest", out string interest))
                    {
                        DisplayBotMessage($"I remember you were interested in {interest}. Would you like to continue learning about that topic?");
                    }
                }
                else
                {
                    DisplayBotMessage("Please enter your name.");
                    userInputTextBox.Tag = "newName";
                }
                ScrollToBottom();
                return;
            }
            else if (userInputTextBox.Tag?.ToString() == "newName")
            {
                userName = userInput;
                userMemory["name"] = userName;
                SaveMemory();
                userInputTextBox.Tag = null;
                DisplayBotMessage($"Hey {userName}, can I assist you with cybersecurity questions, manage tasks, or start a quiz? Try commands like 'add task Enable 2FA' or 'start quiz'.");
                if (userMemory.TryGetValue("interest", out string interest))
                {
                    DisplayBotMessage($"I remember you were interested in {interest}. Would you like to continue learning about that topic?");
                }
                ScrollToBottom();
                return;
            }

            // Handle quiz commands
            if (Regex.IsMatch(userInput.ToLower(), @"^(start|begin|take)\s+quiz$"))
            {
                StartQuiz();
                ScrollToBottom();
                return;
            }
            else if (Regex.IsMatch(userInput.ToLower(), @"^(exit|stop|end)\s+quiz$") && quizMode)
            {
                ExitQuiz();
                ScrollToBottom();
                return;
            }

            // Handle activity log command
            if (Regex.IsMatch(userInput.ToLower(), @"^(show\s+activity\s+log|what\s+have\s+you\s+done\s+for\s+me\?)$"))
            {
                DisplayActionSummary();
                ScrollToBottom();
                return;
            }

            // Handle task-related commands
            if (HandleTaskCommands(userInput))
            {
                ScrollToBottom();
                return;
            }

            conversationHistory.Add(userInput);

            if (userInput.ToLower() == "exit")
            {
                SaveMemory();
                SaveTasks();
                DisplayBotMessage($"Thank you for using Ageisynth AI, {userName}! Stay safe online!");
                Application.Current.Shutdown();
                return;
            }

            if (!string.IsNullOrEmpty(pendingFollowUp))
            {
                if (HandleFollowUpResponse(userInput))
                {
                    ScrollToBottom();
                    return;
                }
            }

            DetectSentiment(userInput);
            if (HandleMemoryQuery(userInput))
            {
                ScrollToBottom();
                return;
            }
            if (HandleInterestDeclaration(userInput))
            {
                SaveMemory();
                ScrollToBottom();
                return;
            }
            string specialResponse = HandleSpecialQuestions(userInput);
            if (specialResponse != null)
            {
                DisplayBotMessage(specialResponse);
                ScrollToBottom();
                return;
            }
            string responseMessage = GenerateKeywordResponse(userInput);
            if (!string.IsNullOrEmpty(responseMessage))
            {
                responseMessage = ApplySentimentContext(responseMessage);
                DisplayBotMessage(responseMessage);
                OfferFollowUp();
            }
            else
            {
                DisplayBotMessage(GetRandomResponse(defaultResponses));
            }
            ScrollToBottom();
        }

        private SentimentPrediction AnalyzeSentiment(string userInput)
        {
            try
            {
                return predEngine.Predict(new SentimentData { Text = userInput });
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"Error analyzing sentiment: {ex.Message}");
                return new SentimentPrediction { Prediction = true, Probability = 0.5f };
            }
        }

        private string GetSentimentResponse(SentimentPrediction prediction)
        {
            float positiveScore = prediction.Probability * 100;
            float negativeScore = 100 - positiveScore;
            string emotionType = prediction.Prediction ? "Positive" : "Negative";

            string feedback = $"Detected {emotionType} Emotion (Positive: {positiveScore:F1}%, Negative: {negativeScore:F1}%)";

            string reply;
            if (positiveScore > 75)
            {
                reply = "You seem really upbeat! Keep shining!";
            }
            else if (positiveScore > 50)
            {
                reply = "You’re doing alright — keep your chin up!";
            }
            else if (positiveScore > 30)
            {
                reply = "I sense some heaviness — it’s okay to feel down sometimes.";
            }
            else
            {
                reply = "You seem quite low. Be kind to yourself — brighter days will come.";
            }

            var result = MessageBox.Show($"{feedback}\nWas this prediction correct?", "Feedback", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                var correct = MessageBox.Show("Was it actually Positive?", "Correct Label", MessageBoxButton.YesNo);
                bool correctLabel = (correct == MessageBoxResult.Yes);
                trainingData.Add(new SentimentData { Text = userInput, Label = correctLabel });
                TrainSentimentModel();
                recentActions.Add("Updated sentiment training data based on user feedback.");
            }

            return reply;
        }

        private void StartQuiz()
        {
            try
            {
                quizMode = true;
                questionIndex = 0;
                currentScore = 0;
                selectedChoice = null;
                correctChoiceButton = null;
                quizPanel.Visibility = Visibility.Visible;
                taskListView.Visibility = Visibility.Collapsed;
                displayScore.Text = "Score: 0";
                submitAnswerButton.IsEnabled = true;
                submitAnswerButton.Content = "Submit Answer";
                DisplayBotMessage("Quiz started! Select an answer and click 'Submit Answer'. Type 'exit quiz' to stop.");
                ShowQuiz();
                recentActions.Add("Quiz started.");
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"Error starting quiz: {ex.Message}");
                quizMode = false;
                quizPanel.Visibility = Visibility.Collapsed;
                taskListView.Visibility = Visibility.Visible;
            }
        }

        private void ExitQuiz()
        {
            quizMode = false;
            quizPanel.Visibility = Visibility.Collapsed;
            taskListView.Visibility = Visibility.Visible;
            DisplayBotMessage("Quiz ended. You can start it again with 'start quiz'.");
            recentActions.Add($"Quiz ended with score {currentScore}/10.");
        }

        private void ShowQuiz()
        {
            if (questionIndex >= quizData.Count)
            {
                string feedback;
                if (currentScore >= 8)
                    feedback = $"Great job, {userName}! You're a cybersecurity pro! Final score: {currentScore}/10.";
                else if (currentScore >= 5)
                    feedback = $"Good effort, {userName}! You scored {currentScore}/10. Keep learning to become a cybersecurity expert!";
                else
                    feedback = $"You scored {currentScore}/10, {userName}. Keep learning to stay safe online! Try the quiz again with 'start quiz'.";
                DisplayBotMessage(feedback);
                quizMode = false;
                quizPanel.Visibility = Visibility.Collapsed;
                taskListView.Visibility = Visibility.Visible;
                recentActions.Add($"Completed quiz with score {currentScore}/10.");
                return;
            }

            correctChoiceButton = null;
            selectedChoice = null;
            var currentQuiz = quizData[questionIndex];
            displayedQuestion.Text = currentQuiz.Question;
            var shuffled = currentQuiz.Choices.OrderBy(_ => Guid.NewGuid()).ToList();
            firstChoiceButton.Content = shuffled[0];
            secondChoiceButton.Content = shuffled[1];
            thirdChoiceButton.Content = shuffled[2];
            fourthChoiceButton.Content = shuffled[3];
            ClearStyle();
        }

        private void ClearStyle()
        {
            foreach (Button choice in new[] { firstChoiceButton, secondChoiceButton, thirdChoiceButton, fourthChoiceButton })
            {
                choice.Background = Brushes.LightGray;
                choice.IsEnabled = true;
            }
            submitAnswerButton.IsEnabled = true;
        }

        public void HandleAnswerSelection(Button selectedButton)
        {
            if (!quizMode) return;
            selectedChoice = selectedButton;
            string chosen = selectedChoice.Content.ToString();
            string correct = quizData[questionIndex].CorrectChoice;
            if (chosen == correct)
            {
                selectedChoice.Background = Brushes.Green;
                correctChoiceButton = selectedChoice;
            }
            else
            {
                selectedChoice.Background = Brushes.DarkRed;
                foreach (Button choice in new[] { firstChoiceButton, secondChoiceButton, thirdChoiceButton, fourthChoiceButton })
                {
                    if (choice.Content.ToString() == correct)
                    {
                        choice.Background = Brushes.Green;
                        correctChoiceButton = choice;
                        break;
                    }
                }
            }
            foreach (Button choice in new[] { firstChoiceButton, secondChoiceButton, thirdChoiceButton, fourthChoiceButton })
            {
                choice.IsEnabled = false;
            }
        }

        public void HandleNextQuestion()
        {
            if (!quizMode)
            {
                DisplayBotMessage("Quiz is not active. Type 'start quiz' to begin.");
                return;
            }
            if (selectedChoice == null)
            {
                DisplayBotMessage("Please select an answer before submitting.");
                return;
            }

            string chosen = selectedChoice.Content.ToString();
            string correct = quizData[questionIndex].CorrectChoice;
            if (chosen == correct)
            {
                currentScore++;
                displayScore.Text = $"Score: {currentScore}";
                DisplayBotMessage($"Correct! {quizData[questionIndex].Explanation}");
            }
            else
            {
                DisplayBotMessage($"Incorrect. {quizData[questionIndex].Explanation}");
            }
            recentActions.Add($"Answered quiz question {questionIndex + 1}: '{chosen}' ({(chosen == correct ? "Correct" : "Incorrect")})");
            questionIndex++;
            ShowQuiz();
        }

        private bool HandleTaskCommands(string userInput)
        {
            if (quizMode)
            {
                DisplayBotMessage("You're in quiz mode. Type 'exit quiz' to manage tasks or ask questions.");
                return true;
            }

            string lowercaseInput = userInput.ToLower().Trim();
            // Patterns for task-related commands
            var addTaskPattern = new Regex(@"^(add|create|new)\s+(task|reminder|todo)\s+(.+)$", RegexOptions.IgnoreCase);
            var setReminderPattern = new Regex(@"^(set|add|create)\s+reminder\s+(.+?)(?:\s+(in|for|on)\s+(\d+)\s+day(s)?|tomorrow)?$", RegexOptions.IgnoreCase);
            var listTasksPattern = new Regex(@"^(list|show)\s+(tasks|todos|reminders)$", RegexOptions.IgnoreCase);
            var deleteTaskPattern = new Regex(@"^(delete|remove)\s+task\s+(.+)$", RegexOptions.IgnoreCase);
            var completeTaskPattern = new Regex(@"^(complete|done|finish)\s+task\s+(.+)$", RegexOptions.IgnoreCase);

            // Add task or reminder
            var addMatch = addTaskPattern.Match(userInput);
            var reminderMatch = setReminderPattern.Match(userInput);
            if (addMatch.Success || reminderMatch.Success)
            {
                string taskTitle = addMatch.Success ? addMatch.Groups[3].Value.Trim() : reminderMatch.Groups[2].Value.Trim();
                DateTime? reminderDate = null;

                if (reminderMatch.Success)
                {
                    if (reminderMatch.Groups[3].Value.ToLower() == "tomorrow")
                    {
                        reminderDate = DateTime.Today.AddDays(1);
                    }
                    else if (reminderMatch.Groups[4].Success && int.TryParse(reminderMatch.Groups[4].Value, out int days))
                    {
                        reminderDate = DateTime.Today.AddDays(days);
                    }
                }

                if (string.IsNullOrWhiteSpace(taskTitle))
                {
                    DisplayBotMessage("Please provide a task or reminder description.");
                    return true;
                }

                string description = $"Complete the task: {taskTitle}";
                if (reminderDate.HasValue)
                {
                    description += $" (Reminder set for {reminderDate.Value:yyyy-MM-dd})";
                }

                var task = new Task
                {
                    Title = taskTitle,
                    Description = description,
                    ReminderDate = reminderDate,
                    Status = "Pending"
                };
                tasks.Add(task);
                taskListView.Items.Add(task);
                taskListView.ScrollIntoView(task);
                SaveTasks();
                string actionMessage = $"Task added: '{taskTitle}'. {(reminderDate.HasValue ? $"Reminder set for {reminderDate.Value:yyyy-MM-dd}." : "No reminder set.")}";
                DisplayBotMessage(actionMessage);
                recentActions.Add(actionMessage);
                if (!reminderDate.HasValue)
                {
                    DisplayBotMessage("Would you like to set a reminder for this task? (e.g., 'set reminder in 3 days' or 'set reminder tomorrow')");
                    userInputTextBox.Tag = $"setReminder:{tasks.Count - 1}";
                }
                else
                {
                    userInputTextBox.Tag = null;
                }
                return true;
            }

            // Set reminder for existing task
            if (userInputTextBox.Tag?.ToString().StartsWith("setReminder:") == true && setReminderPattern.IsMatch(userInput))
            {
                if (int.TryParse(userInputTextBox.Tag.ToString().Split(':')[1], out int taskIndex) && taskIndex < tasks.Count)
                {
                    var match = setReminderPattern.Match(userInput);
                    var task = tasks[taskIndex];
                    if (match.Groups[3].Value.ToLower() == "tomorrow")
                    {
                        task.ReminderDate = DateTime.Today.AddDays(1);
                    }
                    else if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out int days))
                    {
                        task.ReminderDate = DateTime.Today.AddDays(days);
                    }
                    if (task.ReminderDate.HasValue)
                    {
                        task.Description = $"Complete the task: {task.Title} (Reminder set for {task.ReminderDate.Value:yyyy-MM-dd})";
                        taskListView.Items.Refresh();
                        SaveTasks();
                        string actionMessage = $"Reminder set for '{task.Title}' on {task.ReminderDate.Value:yyyy-MM-dd}.";
                        DisplayBotMessage(actionMessage);
                        recentActions.Add(actionMessage);
                        userInputTextBox.Tag = null;
                    }
                    else
                    {
                        DisplayBotMessage("Please specify reminder in format: 'set reminder in X days' or 'set reminder tomorrow'.");
                    }
                    return true;
                }
            }

            // List tasks
            if (listTasksPattern.IsMatch(userInput))
            {
                if (tasks.Count == 0)
                {
                    DisplayBotMessage("No tasks available.");
                }
                else
                {
                    DisplayBotMessage("Your cybersecurity tasks:");
                    foreach (var task in tasks)
                    {
                        DisplayBotMessage(task.ToString());
                    }
                }
                recentActions.Add("Displayed task list.");
                return true;
            }

            // Delete task
            if (deleteTaskPattern.IsMatch(userInput))
            {
                string taskTitle = deleteTaskPattern.Match(userInput).Groups[2].Value.Trim();
                var task = tasks.FirstOrDefault(t => t.Title.Equals(taskTitle, StringComparison.OrdinalIgnoreCase));
                if (task != null)
                {
                    tasks.Remove(task);
                    taskListView.Items.Remove(task);
                    SaveTasks();
                    string actionMessage = $"Task '{taskTitle}' deleted.";
                    DisplayBotMessage(actionMessage);
                    recentActions.Add(actionMessage);
                }
                else
                {
                    DisplayBotMessage($"Task '{taskTitle}' not found.");
                }
                return true;
            }

            // Complete task
            if (completeTaskPattern.IsMatch(userInput))
            {
                string taskTitle = completeTaskPattern.Match(userInput).Groups[2].Value.Trim();
                var task = tasks.FirstOrDefault(t => t.Title.Equals(taskTitle, StringComparison.OrdinalIgnoreCase));
                if (task != null)
                {
                    task.Status = "Completed";
                    taskListView.Items.Refresh();
                    SaveTasks();
                    string actionMessage = $"Task '{taskTitle}' marked as completed.";
                    DisplayBotMessage(actionMessage);
                    recentActions.Add(actionMessage);
                }
                else
                {
                    DisplayBotMessage($"Task '{taskTitle}' not found.");
                }
                return true;
            }

            return false;
        }

        private void DisplayActionSummary()
        {
            if (recentActions.Count == 0)
            {
                DisplayBotMessage("No recent actions recorded.");
                return;
            }

            DisplayBotMessage("Here’s a summary of recent actions:");
            for (int i = 0; i < Math.Min(recentActions.Count, 5); i++)
            {
                DisplayBotMessage($"{i + 1}. {recentActions[recentActions.Count - 1 - i]}");
            }
        }

        public void ToggleTaskStatus(Task task)
        {
            if (task.Status == "Pending")
            {
                task.Status = "Completed";
                string actionMessage = $"Task '{task.Title}' marked as completed.";
                DisplayBotMessage(actionMessage);
                recentActions.Add(actionMessage);
            }
            else
            {
                tasks.Remove(task);
                taskListView.Items.Remove(task);
                string actionMessage = $"Task '{task.Title}' deleted.";
                DisplayBotMessage(actionMessage);
                recentActions.Add(actionMessage);
            }
            taskListView.Items.Refresh();
            SaveTasks();
        }

        public void ClearCompletedTasks()
        {
            var completedTasks = tasks.Where(t => t.Status == "Completed").ToList();
            if (completedTasks.Count == 0)
            {
                DisplayBotMessage("No completed tasks to clear.");
                return;
            }
            foreach (var task in completedTasks)
            {
                tasks.Remove(task);
                taskListView.Items.Remove(task);
            }
            SaveTasks();
            string actionMessage = "All completed tasks cleared.";
            DisplayBotMessage(actionMessage);
            recentActions.Add(actionMessage);
        }

        public void CheckReminders()
        {
            var now = DateTime.Now;
            var dueTasks = tasks.Where(t => t.ReminderDate.HasValue && t.ReminderDate.Value <= now && t.Status == "Pending").ToList();
            foreach (var task in dueTasks)
            {
                string actionMessage = $"Reminder: Task '{task.Title}' is due! ({task.ReminderDate.Value:yyyy-MM-dd})";
                DisplayBotMessage(actionMessage);
                recentActions.Add(actionMessage);
            }
        }

        private bool HandleFollowUpResponse(string userInput)
        {
            string lowercaseInput = userInput.ToLower().Trim();
            if (Regex.IsMatch(lowercaseInput, @"^(yes|yeah|yep|sure|ok|okay)$"))
            {
                if (followUpResponses.ContainsKey(pendingFollowUp) &&
                    followUpResponses[pendingFollowUp].ContainsKey("yes"))
                {
                    DisplayBotMessage(followUpResponses[pendingFollowUp]["yes"]);
                    recentActions.Add($"Responded to follow-up question '{pendingFollowUp}' with 'yes'.");
                    pendingFollowUp = string.Empty;
                    return true;
                }
            }
            else if (Regex.IsMatch(lowercaseInput, @"^(no|nope|nah)$"))
            {
                if (followUpResponses.ContainsKey(pendingFollowUp) &&
                    followUpResponses[pendingFollowUp].ContainsKey("no"))
                {
                    DisplayBotMessage(followUpResponses[pendingFollowUp]["no"]);
                    recentActions.Add($"Responded to follow-up question '{pendingFollowUp}' with 'no'.");
                    pendingFollowUp = string.Empty;
                    return true;
                }
            }
            pendingFollowUp = string.Empty;
            return false;
        }

        private void DetectSentiment(string userInput)
        {
            string lowercaseInput = userInput.ToLower();
            foreach (string sentiment in detectedSentiments.Keys.ToList())
            {
                detectedSentiments[sentiment] = false;
            }
            if (lowercaseInput.Contains("worried") || lowercaseInput.Contains("afraid") ||
                lowercaseInput.Contains("scared") || lowercaseInput.Contains("fear"))
            {
                detectedSentiments["worried"] = true;
            }
            if (lowercaseInput.Contains("confused") || lowercaseInput.Contains("don't understand") ||
                lowercaseInput.Contains("unclear") || lowercaseInput.Contains("complicated"))
            {
                detectedSentiments["confused"] = true;
            }
            if (lowercaseInput.Contains("frustrated") || lowercaseInput.Contains("annoyed") ||
                lowercaseInput.Contains("upset") || lowercaseInput.Contains("angry"))
            {
                detectedSentiments["frustrated"] = true;
            }
            if (lowercaseInput.Contains("curious") || lowercaseInput.Contains("interested") ||
                lowercaseInput.Contains("want to know") || lowercaseInput.Contains("tell me more"))
            {
                detectedSentiments["curious"] = true;
            }
            if (lowercaseInput.Contains("happy") || lowercaseInput.Contains("glad") ||
                lowercaseInput.Contains("great") || lowercaseInput.Contains("excellent"))
            {
                detectedSentiments["happy"] = true;
            }
        }

        private string ApplySentimentContext(string baseResponse)
        {
            string modifiedResponse = baseResponse;
            if (detectedSentiments["worried"])
            {
                modifiedResponse = "I understand you might be worried about " + baseResponse +
                    " Remember that taking small steps to improve your security can make a big difference.";
            }
            else if (detectedSentiments["confused"])
            {
                modifiedResponse = "This topic can be confusing, so let me explain it simply. " + baseResponse +
                    " Would you like me to clarify anything specific?";
            }
            else if (detectedSentiments["frustrated"])
            {
                modifiedResponse = "I can see this might be frustrating. " + baseResponse +
                    " Let's take this one step at a time to make it more manageable.";
            }
            else if (detectedSentiments["curious"])
            {
                modifiedResponse = "I'm glad you're curious about this! " + baseResponse +
                    " Learning about cybersecurity is an important step toward staying safe online.";
            }
            else if (detectedSentiments["happy"])
            {
                modifiedResponse = "Great! I'm happy to share this information with you. " + baseResponse +
                    " It's always good to see someone enthusiastic about cybersecurity!";
            }
            return modifiedResponse;
        }

        private bool HandleInterestDeclaration(string userInput)
        {
            string lowercaseInput = userInput.ToLower();
            if (lowercaseInput.Contains("interested in") || lowercaseInput.Contains("curious about"))
            {
                foreach (string topic in topicResponses.Keys)
                {
                    if (lowercaseInput.Contains(topic))
                    {
                        userMemory["interest"] = topic;
                        DisplayBotMessage($"Great! I'll remember that you're interested in {topic}. It's an important aspect of cybersecurity!");
                        string topicResponse = GetRandomResponse(topicResponses[topic]);
                        DisplayBotMessage($"Here's something about {topic}: {topicResponse}");
                        recentActions.Add($"Noted interest in '{topic}'.");
                        return true;
                    }
                }
            }
            return false;
        }

        private bool HandleMemoryQuery(string userInput)
        {
            string lowercaseInput = userInput.ToLower();
            if (lowercaseInput.Contains("what was i interested in") && userMemory.ContainsKey("interest"))
            {
                DisplayBotMessage($"You previously mentioned an interest in {userMemory["interest"]}. Would you like to know more about it?");
                recentActions.Add("Queried user interest.");
                return true;
            }
            if (userMemory.ContainsKey("interest") && lowercaseInput.Contains(userMemory["interest"]))
            {
                string interest = userMemory["interest"];
                if (topicResponses.ContainsKey(interest))
                {
                    string response = GetRandomResponse(topicResponses[interest]);
                    DisplayBotMessage($"As someone interested in {interest}, you might find this helpful: {response}");
                    recentActions.Add($"Provided info on interest '{interest}'.");
                    return true;
                }
            }
            if (lowercaseInput.Contains("what do you know about me") ||
                lowercaseInput.Contains("what do you remember") ||
                lowercaseInput.Contains("my information"))
            {
                if (userMemory.Count > 0)
                {
                    DisplayBotMessage($"Here's what I remember about you, {userName}:");
                    foreach (var item in userMemory)
                    {
                        if (item.Key != "name")
                        {
                            DisplayBotMessage($"- Your {item.Key}: {item.Value}");
                        }
                    }
                    recentActions.Add("Displayed user memory.");
                    return true;
                }
                else
                {
                    DisplayBotMessage($"I only know your name is {userName}. You can tell me about your interests in cybersecurity topics, and I'll remember them for next time!");
                    recentActions.Add("Displayed user memory (empty).");
                    return true;
                }
            }
            return false;
        }

        private string HandleSpecialQuestions(string userInput)
        {
            string lowercaseInput = userInput.ToLower();
            foreach (var question in specialQuestions)
            {
                if (lowercaseInput.Contains(question.Key))
                {
                    recentActions.Add($"Answered special question '{question.Key}'.");
                    return question.Value;
                }
            }
            return null;
        }

        private string GenerateKeywordResponse(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return GetRandomResponse(defaultResponses);
            }
            string[] inputWords = userInput.ToLower().Split(new[] { ' ', ',', '.', '?', '!' },
                StringSplitOptions.RemoveEmptyEntries);
            List<string> relevantWords = inputWords.Where(word => !ignoreWords.Contains(word)).ToList();
            List<string> detectedTopics = new List<string>();
            foreach (string word in relevantWords)
            {
                foreach (string topic in topicResponses.Keys)
                {
                    if (topic.Contains(word) || word.Contains(topic))
                    {
                        detectedTopics.Add(topic);
                        currentTopic = topic;
                    }
                }
            }
            if (detectedTopics.Count == 0 && !string.IsNullOrEmpty(currentTopic) &&
                (userInput.ToLower().Contains("more") || userInput.ToLower().Contains("tell me") ||
                 userInput.ToLower().Contains("what about")))
            {
                detectedTopics.Add(currentTopic);
            }
            if (detectedTopics.Count == 0)
            {
                return string.Empty;
            }
            string combinedResponse = string.Empty;
            foreach (string topic in detectedTopics.Distinct())
            {
                if (topicResponses.ContainsKey(topic))
                {
                    string topicResponse = GetRandomResponse(topicResponses[topic]);
                    combinedResponse += $"{topic}: {topicResponse}\n\n";
                    recentActions.Add($"Provided info on topic '{topic}'.");
                }
            }
            return combinedResponse.Trim();
        }

        private void OfferFollowUp()
        {
            if (string.IsNullOrEmpty(currentTopic))
            {
                return;
            }
            Dictionary<string, List<string>> followUps = new Dictionary<string, List<string>>
            {
                { "password", new List<string> {
                    "Would you like to know more about password managers?",
                    "Are you using two-factor authentication for your accounts?",
                    "Do you want tips on creating strong passwords that are easy to remember?"
                }},
                { "phishing", new List<string> {
                    "Would you like to know how to identify suspicious emails?",
                    "Do you know what to do if you suspect you've received a phishing attempt?",
                    "Are you familiar with the latest phishing techniques?"
                }},
                { "privacy", new List<string> {
                    "Would you like to review your social media privacy settings?",
                    "Do you know how data brokers collect and sell your information?",
                    "Are you interested in tools that can help protect your online privacy?"
                }},
                { "malware", new List<string> {
                    "Would you like to know the warning signs of malware infection?",
                    "Do you have anti-malware software installed?",
                    "Are you familiar with ransomware and how to protect against it?"
                }},
                { "firewall", new List<string> {
                    "Would you like to know more about configuring your firewall?",
                    "Do you understand the difference between hardware and software firewalls?",
                    "Are you checking your firewall logs regularly?"
                }}
            };
            if (followUps.ContainsKey(currentTopic))
            {
                string followUpQuestion = GetRandomResponse(followUps[currentTopic]);
                DisplayBotMessage(followUpQuestion);
                pendingFollowUp = followUpQuestion;
                recentActions.Add($"Offered follow-up question '{followUpQuestion}'.");
            }
        }

        private void LoadQuizData()
        {
            quizData = new List<QuizQuestion>
            {
                new QuizQuestion
                {
                    Question = "What should you do if you receive an email asking for your password?",
                    Choices = new List<string> { "Reply with your password", "Delete the email", "Report the email as phishing", "Ignore it" },
                    CorrectChoice = "Report the email as phishing",
                    Explanation = "Reporting phishing emails helps prevent scams and protects others. Never share your password via email."
                },
                new QuizQuestion
                {
                    Question = "A strong password should be at least how many characters long?",
                    Choices = new List<string> { "6", "8", "12", "16" },
                    CorrectChoice = "12",
                    Explanation = "A password of at least 12 characters with a mix of letters, numbers, and symbols is considered strong."
                },
                new QuizQuestion
                {
                    Question = "True or False: Using the same password for multiple accounts is safe.",
                    Choices = new List<string> { "True", "False", "Sometimes", "Only for social media" },
                    CorrectChoice = "False",
                    Explanation = "Using the same password across multiple accounts increases the risk of multiple accounts being compromised if one is breached."
                },
                new QuizQuestion
                {
                    Question = "What is a common sign of a phishing email?",
                    Choices = new List<string> { "Personalized greeting", "Urgent language or threats", "Official company logo", "Correct grammar" },
                    CorrectChoice = "Urgent language or threats",
                    Explanation = "Phishing emails often use urgent language or threats to create panic and prompt quick action without thinking."
                },
                new QuizQuestion
                {
                    Question = "Which is a safe browsing habit?",
                    Choices = new List<string> { "Clicking all pop-up ads", "Downloading files from unknown sources", "Using HTTPS websites", "Sharing personal info on forums" },
                    CorrectChoice = "Using HTTPS websites",
                    Explanation = "HTTPS websites encrypt data, making them more secure for browsing and entering sensitive information."
                },
                new QuizQuestion
                {
                    Question = "What does two-factor authentication (2FA) require?",
                    Choices = new List<string> { "Two passwords", "A password and a second factor like a code", "Two email addresses", "A password and a PIN" },
                    CorrectChoice = "A password and a second factor like a code",
                    Explanation = "2FA requires something you know (password) and something you have (e.g., a code sent to your phone) for extra security."
                },
                new QuizQuestion
                {
                    Question = "True or False: Public Wi-Fi is always safe for accessing sensitive accounts.",
                    Choices = new List<string> { "True", "False", "Only at coffee shops", "Only if password-protected" },
                    CorrectChoice = "False",
                    Explanation = "Public Wi-Fi can be insecure. Use a VPN or avoid sensitive transactions unless the network is secure."
                },
                new QuizQuestion
                {
                    Question = "What is social engineering?",
                    Choices = new List<string> { "Hacking a computer", "Manipulating people to gain information", "Installing software", "Configuring firewalls" },
                    CorrectChoice = "Manipulating people to gain information",
                    Explanation = "Social engineering involves tricking people into revealing sensitive information, often through deception."
                },
                new QuizQuestion
                {
                    Question = "What should you do if you suspect malware on your device?",
                    Choices = new List<string> { "Ignore it", "Run a full system scan", "Reboot repeatedly", "Share it with friends" },
                    CorrectChoice = "Run a full system scan",
                    Explanation = "Running a full system scan with antivirus software can detect and remove malware."
                },
                new QuizQuestion
                {
                    Question = "Which is a benefit of using a password manager?",
                    Choices = new List<string> { "Stores only one password", "Generates and stores strong passwords", "Sends passwords via email", "Disables 2FA" },
                    CorrectChoice = "Generates and stores strong passwords",
                    Explanation = "Password managers generate, store, and autofill strong, unique passwords, enhancing security."
                }
            };
        }

        private string GetRandomResponse(List<string> responses)
        {
            if (responses == null || responses.Count == 0)
            {
                return "I don't have information on that topic yet.";
            }
            Random random = new Random();
            int index = random.Next(responses.Count);
            return responses[index];
        }

        private void SaveMemory()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var item in userMemory)
                {
                    lines.Add($"{item.Key}={item.Value}");
                }
                File.WriteAllLines(MEMORY_FILE, lines);
                DisplayBotMessage("[Memory saved]");
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"[Error saving memory: {ex.Message}]");
            }
        }

        private void LoadMemory()
        {
            try
            {
                if (File.Exists(MEMORY_FILE))
                {
                    string[] lines = File.ReadAllLines(MEMORY_FILE);
                    userMemory.Clear();
                    foreach (string line in lines)
                    {
                        int separatorIndex = line.IndexOf('=');
                        if (separatorIndex > 0)
                        {
                            string key = line.Substring(0, separatorIndex);
                            string value = line.Substring(separatorIndex + 1);
                            userMemory[key] = value;
                        }
                    }
                    DisplayBotMessage("[Memory loaded]");
                }
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"[Error loading memory: {ex.Message}]");
                userMemory.Clear();
            }
        }

        private void SaveTasks()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var task in tasks)
                {
                    lines.Add($"{task.Title}|{task.Description}|{(task.ReminderDate.HasValue ? task.ReminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "")}|{task.Status}");
                }
                File.WriteAllLines(TASKS_FILE, lines);
                DisplayBotMessage("[Tasks saved]");
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"[Error saving tasks: {ex.Message}]");
            }
        }

        private void LoadTasks()
        {
            try
            {
                if (File.Exists(TASKS_FILE))
                {
                    string[] lines = File.ReadAllLines(TASKS_FILE);
                    tasks.Clear();
                    taskListView.Items.Clear();
                    foreach (string line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length == 4)
                        {
                            var task = new Task
                            {
                                Title = parts[0],
                                Description = parts[1],
                                ReminderDate = string.IsNullOrEmpty(parts[2]) ? null : (DateTime?)DateTime.Parse(parts[2]),
                                Status = parts[3]
                            };
                            tasks.Add(task);
                            taskListView.Items.Add(task);
                        }
                    }
                    DisplayBotMessage("[Tasks loaded]");
                }
            }
            catch (Exception ex)
            {
                DisplayBotMessage($"[Error loading tasks: {ex.Message}]");
                tasks.Clear();
                taskListView.Items.Clear();
            }
        }

        public void DisplayBotMessage(string message)
        {
            TextBlock botMessage = new TextBlock
            {
                Text = $"AgeisynthBot: {message}",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            chatPanel.Children.Add(botMessage);
        }

        private void ScrollToBottom()
        {
            var scrollViewer = chatPanel.Parent as ScrollViewer;
            scrollViewer?.ScrollToBottom();
        }

        private void InitializeIgnoreWords()
        {
            string[] words = {
                "tell", "and", "me", "about", "ensure", "for", "how", "what", "is", "you", "your", "the", "can",
                "do", "would", "will", "should", "could", "are", "am", "was", "were", "be", "been", "being", "it",
                "that", "this", "these", "those", "here", "there", "where", "when", "why", "who", "which", "um",
                "uh", "no", "like", "know", "so", "actually", "okay", "sure", "yeah", "yep", "nope", "nah", "not",
                "very", "quite", "slightly", "a", "an", "in", "on", "at", "by", "of", "to", "with", "my", "i",
                "we", "they", "us", "it's", "that's", "don't", "won't", "can't", "doesn't", "didn't", "some",
                "any", "please", "thanks", "thank", "more", "give", "need", "want", "have", "has", "had", "get"
            };
            foreach (string word in words)
            {
                ignoreWords.Add(word);
            }
        }

        private void InitializeResponses()
        {
            topicResponses["password"] = new List<string>
            {
                "Make sure to use strong, unique passwords for each account. Aim for at least 12 characters with a mix of letters, numbers, and symbols.",
                "Consider using a password manager like LastPass, Bitwarden, or 1Password to generate and store complex passwords.",
                "Enable two-factor authentication whenever possible to add an extra layer of security beyond just your password.",
                "Avoid using personal information in your passwords, such as birthdays, names, or addresses that could be easily guessed.",
                "Change your critical passwords (email, banking) every 3-6 months to minimize the risk of breaches."
            };
            topicResponses["phishing"] = new List<string>
            {
                "Be cautious of emails asking for personal information. Legitimate organizations rarely request sensitive details via email.",
                "Check email sender addresses carefully - phishers often use addresses that look similar to legitimate ones but with small changes.",
                "Hover over links before clicking to see where they actually lead. If in doubt, navigate to the website directly instead of clicking.",
                "Be wary of urgent requests or threats - phishers often create a false sense of urgency to make you act without thinking.",
                "If an email offer seems too good to be true, it probably is. Be skeptical of unexpected prizes or rewards."
            };
            topicResponses["privacy"] = new List<string>
            {
                "Regularly review and update privacy settings on all your social media accounts to control what information is visible to others.",
                "Use private browsing modes and consider a VPN when accessing sensitive information on public networks.",
                "Be cautious about what personal information you share online - once it's out there, it can be difficult to remove.",
                "Regularly check for data breaches involving your accounts using services like Have I Been Pwned.",
                "Consider using privacy-focused alternatives to common services, such as DuckDuckGo instead of Google for searching."
            };
            topicResponses["malware"] = new List<string>
            {
                "Keep your operating system and software updated to patch security vulnerabilities that malware can exploit.",
                "Use reputable antivirus and anti-malware software, and ensure it's set to update and scan regularly.",
                "Be cautious when downloading files or clicking on links, especially from unknown or untrusted sources.",
                "Back up your important data regularly to external devices or cloud services that aren't continuously connected to your computer.",
                "Be wary of unexpected pop-ups, system slowdowns, or unusual behavior - these could be signs of malware infection."
            };
            topicResponses["firewall"] = new List<string>
            {
                "Ensure your firewall is enabled at all times to filter network traffic and block unauthorized access attempts.",
                "Regularly review your firewall settings to ensure they're configured properly for your needs.",
                "Consider using both hardware (router) and software (OS) firewalls for multiple layers of protection.",
                "When installing new software, be cautious about allowing it through your firewall - only grant access if necessary.",
                "For higher security, configure your firewall to use a default-deny policy, where only explicitly allowed connections are permitted."
            };
            topicResponses["scam"] = new List<string>
            {
                "Be skeptical of unsolicited phone calls, emails, or messages, especially those requesting personal information or payment.",
                "Research unfamiliar companies or offers thoroughly before providing any information or making payments.",
                "Use secure payment methods that offer protection, such as credit cards or PayPal, rather than wire transfers or gift cards.",
                "Trust your instincts - if something feels wrong or too good to be true, it's better to walk away.",
                "Keep up with current scam techniques by checking resources like the FTC's scam alerts website."
            };
            topicResponses["attack"] = new List<string>
            {
                "Keep all your devices and software updated to protect against known vulnerabilities.",
                "Use strong authentication methods, including multi-factor authentication, for all important accounts.",
                "Be aware of social engineering tactics - attackers often manipulate people rather than technology.",
                "Segment your network to limit access between different systems and contain potential breaches.",
                "Consider implementing an intrusion detection system to alert you to suspicious activities."
            };
            topicResponses["sql"] = new List<string>
            {
                "Always validate and sanitize user input to prevent malicious SQL commands from being executed.",
                "Use parameterized queries or prepared statements instead of directly concatenating user input into SQL queries.",
                "Implement the principle of least privilege for database accounts to minimize damage if a breach occurs.",
                "Regularly audit your database and application code for potential SQL injection vulnerabilities.",
                "Consider using an ORM (Object-Relational Mapping) framework which can help prevent SQL injection by design."
            };
        }

        private void InitializeSpecialQuestions()
        {
            specialQuestions["how are you"] = "I'm a cybersecurity chatbot, always ready to help you stay safe online! How can I assist you today?";
            specialQuestions["what is your purpose"] = "My purpose is to provide helpful cybersecurity advice and raise awareness about online safety. I can answer questions about passwords, phishing, privacy, malware, and other security topics.";
            specialQuestions["what can i ask about"] = "You can ask me about various cybersecurity topics including:\n" +
                "- Password safety and management\n" +
                "- Phishing attacks and how to avoid them\n" +
                "- Privacy protection online\n" +
                "- Malware prevention and detection\n" +
                "- Firewall configuration\n" +
                "- Scam awareness\n" +
                "- SQL injection and other technical attacks\n\n" +
                "You can also manage tasks with commands like 'add task', 'list tasks', 'complete task', or 'delete task', or start a quiz with 'start quiz'.";
            specialQuestions["who made you"] = "I was created as part of the Ageisynth Cybersecurity Awareness initiative to help people learn about staying safe online.";
            specialQuestions["help"] = "I can provide information on cybersecurity topics like passwords, phishing, privacy, malware, and more. You can also manage tasks with commands like 'add task Enable 2FA', 'list tasks', 'complete task', or 'delete task', or start a quiz with 'start quiz'. Just ask me a question or use a command!";
            specialQuestions["how do i exit"] = "To exit the program, type 'exit' in the input box. To exit the quiz, type 'exit quiz'.";
        }

        private void InitializeFollowUpResponses()
        {
            followUpResponses["Do you know what to do if you suspect you've received a phishing attempt?"] = new Dictionary<string, string>
            {
                ["yes"] = "That's great! It's important to be prepared. Just as a reminder, you should: 1) Don't click any links or download attachments, 2) Don't reply with personal information, 3) Report the email to your IT department if at work, or forward to the organization being impersonated, 4) Delete the email, and 5) Consider running a security scan on your device.",
                ["no"] = "If you receive a suspected phishing email: 1) Don't click any links or download attachments, 2) Don't reply with personal information, 3) Report the email to your IT department if at work, or forward to the organization being impersonated via their official contact channels, 4) Delete the email, and 5) Consider running a security scan on your device to be safe."
            };
            followUpResponses["Would you like to know more about password managers?"] = new Dictionary<string, string>
            {
                ["yes"] = "Password managers are tools that securely store and manage your passwords. They can generate strong, unique passwords for all your accounts and auto-fill them when needed. Popular options include LastPass, 1Password, Bitwarden, and KeePass. They use strong encryption to protect your data, and you only need to remember one master password. Many also offer secure sharing, breach monitoring, and multi-factor authentication.",
                ["no"] = "No problem! If you ever change your mind, just ask about password managers. Meanwhile, remember to use strong, unique passwords for all your accounts, and consider enabling two-factor authentication when available."
            };
            followUpResponses["Are you using two-factor authentication for your accounts?"] = new Dictionary<string, string>
            {
                ["yes"] = "Excellent! Using two-factor authentication is one of the best ways to protect your accounts. Remember to keep your authentication app or backup codes in a secure place in case you lose your phone or primary authentication device.",
                ["no"] = "I'd strongly recommend setting up two-factor authentication (2FA) for your important accounts. It adds an extra layer of security by requiring something you know (password) and something you have (like your phone). Even if someone steals your password, they can't access your account without the second factor. Most email providers, social media platforms, and financial services offer 2FA options in their security settings."
            };
            followUpResponses["Do you want tips on creating strong passwords that are easy to remember?"] = new Dictionary<string, string>
            {
                ["yes"] = "Great! Here are some tips for creating strong, memorable passwords: 1) Use a passphrase - a sequence of random words (e.g., 'correct-horse-battery-staple'), 2) Add numbers and special characters between words, 3) Use the first letters of a meaningful sentence (e.g., 'MdGTFaFw25y!' for 'My dog Gizmo turned five and found wisdom 25 years!'), 4) Avoid personal information, and 5) Don't reuse passwords across different accounts.",
                ["no"] = "That's fine! Remember that a password manager can generate and remember strong passwords for you. If you ever need password tips in the future, just ask."
            };
            followUpResponses["Would you like to know how to identify suspicious emails?"] = new Dictionary<string, string>
            {
                ["yes"] = "Here are key signs of suspicious emails: 1) Mismatched or strange sender email addresses, 2) Generic greetings like 'Dear User' instead of your name, 3) Poor grammar or spelling errors, 4) A sense of urgency or threats, 5) Requests for personal information, 6) Suspicious attachments or links, and 7) Offers that seem too good to be true. Always hover over links before clicking to see the actual URL destination.",
                ["no"] = "No problem! If you ever need help identifying suspicious emails in the future, just let me know."
            };
            followUpResponses["Are you familiar with the latest phishing techniques?"] = new Dictionary<string, string>
            {
                ["yes"] = "That's great! Staying informed about the latest threats is important. Remember that phishing techniques are constantly evolving, so it's good to regularly check security news sources to stay up-to-date.",
                ["no"] = "Recent phishing techniques include: 1) Spear phishing - highly targeted attacks using personal information, 2) Clone phishing - duplicating legitimate emails but replacing links with malicious ones, 3) Voice phishing (vishing) - phone calls pretending to be legitimate companies, 4) SMS phishing (smishing) - text messages with malicious links, 5) Business Email Compromise (BEC) - impersonating executives to request wire transfers, and 6) QR code phishing - malicious QR codes leading to fake websites."
            };
            followUpResponses["Would you like to review your social media privacy settings?"] = new Dictionary<string, string>
            {
                ["yes"] = "Great! Here are key privacy settings to check on social media: 1) Profile visibility - limit who can see your profile and posts, 2) Friend/connection permissions - control what connections can see, 3) Post audience settings - choose who sees each post, 4) Tag settings - review tags before they appear on your profile, 5) Search visibility - control whether search engines can find you, and 6) App permissions - review which third-party apps have access to your account.",
                ["no"] = "No problem! If you ever want to review your social media privacy settings in the future, ask for guidance."
            };
            followUpResponses["Do you know how data brokers collect and sell your information?"] = new Dictionary<string, string>
            {
                ["yes"] = "It's good that you're informed about data brokers. Remember that you can opt out of many data broker services, though it may require contacting each company individually.",
                ["no"] = "Data brokers collect information about you from various sources including public records, online activities, purchase histories, social media, and app usage. They compile this into detailed profiles and sell it to advertisers, marketers, other businesses, and sometimes individuals. To protect yourself, you can: 1) Opt out of data collection when possible, 2) Use privacy-focused browsers and search engines, 3) Regularly check and adjust privacy settings on your accounts, and 4) Consider using services that contact data brokers to remove your information."
            };
            followUpResponses["Are you interested in tools that can help protect your online privacy?"] = new Dictionary<string, string>
            {
                ["yes"] = "Great! Here are some helpful privacy tools: 1) VPNs (Virtual Private Networks) to encrypt your internet traffic, 2) Privacy-focused browsers like Firefox or Brave, 3) Ad and tracker blockers like uBlock Origin or Privacy Badger, 4) Secure messaging apps like Signal or Wickr, 5) Password managers, 6) Email aliases or forwarding services to hide your real email, and 7) Privacy-focused search engines like DuckDuckGo or Startpage.",
                ["no"] = "That's okay! If you ever become interested in privacy tools in the future, feel free to ask about them."
            };
            followUpResponses["Would you like to know the warning signs of malware infection?"] = new Dictionary<string, string>
            {
                ["yes"] = "Here are common signs of malware infection: 1) Unexpected slowdowns or crashes, 2) Pop-up ads even when browsers are closed, 3) Changes to your homepage or browser settings, 4) Unfamiliar programs running or in your app list, 5) Disabled security software, 6) Unusual network activity or data usage, 7) Missing files or storage space, 8) Overheating computer, 9) Friends receiving strange messages from your accounts, and 10) Unexpected system behavior like random restarts.",
                ["no"] = "No problem! If you notice unusual behavior on your device in the future and want to check if it might be malware, just ask."
            };
            followUpResponses["Do you have anti-malware software installed?"] = new Dictionary<string, string>
            {
                ["yes"] = "Excellent! Make sure your anti-malware software is set to update automatically and perform regular scans. Remember that no protection is 100% effective, so safe browsing habits are still important.",
                ["no"] = "I'd strongly recommend installing reputable anti-malware software. While Windows Defender (built into Windows) provides basic protection, additional options include Malwarebytes, Bitdefender, and Kaspersky. Look for software that offers real-time protection, automatic updates, scheduled scanning, and web protection features. Many offer free basic versions that provide essential protection."
            };
            followUpResponses["Are you familiar with ransomware and how to protect against it?"] = new Dictionary<string, string>
            {
                ["yes"] = "Great! It's good that you're aware of ransomware threats. Remember that regular backups are your best defense against ransomware attacks.",
                ["no"] = "Ransomware is malware that encrypts your files and demands payment for the decryption key. To protect yourself: 1) Keep regular backups of important files on disconnected storage, 2) Keep your operating system and software updated, 3) Be cautious with email attachments and downloads, 4) Use anti-malware software with ransomware protection, 5) Apply the principle of least privilege for user accounts, and 6) Consider using ransomware-specific protection tools. If infected, disconnect from the internet immediately and seek professional help."
            };
            followUpResponses["Would you like to know more about configuring your firewall?"] = new Dictionary<string, string>
            {
                ["yes"] = "Here are firewall configuration tips: 1) Ensure your firewall is enabled on all networks, 2) Create separate rules for public and private networks, 3) Block all incoming connections by default, then add exceptions only as needed, 4) Use application-based rules rather than port-based when possible, 5) Log blocked connection attempts, 6) Regularly review and remove unused rules, and 7) Test your configuration with online firewall testing tools. Most operating systems have built-in firewalls with configuration guides available.",
                ["no"] = "No problem! If you need firewall configuration help in the future, feel free to ask."
            };
            followUpResponses["Do you understand the difference between hardware and software firewalls?"] = new Dictionary<string, string>
            {
                ["yes"] = "Great! Using both hardware and software firewalls provides layered protection, which is an excellent security practice.",
                ["no"] = "Hardware firewalls are physical devices (usually built into routers) that filter traffic before it reaches any device on your network. They protect all connected devices at once. Software firewalls are programs installed on individual devices that filter traffic specific to that device. They can provide more granular control over applications. For best protection, use both: hardware firewalls to protect your entire network and software firewalls for device-specific protection."
            };
            followUpResponses["Are you checking your firewall logs regularly?"] = new Dictionary<string, string>
            {
                ["yes"] = "Excellent practice! Regular log checking helps you spot unusual patterns that might indicate attempted intrusions.",
                ["no"] = "Checking firewall logs can help identify potential threats and attempted intrusions. Look for: 1) Multiple failed access attempts from the same source, 2) Connection attempts to unusual ports, 3) High traffic volumes at unexpected times, and 4) Connection attempts from suspicious geographical locations. Most operating systems let you access firewall logs through security settings. For routers, check the admin panel. Consider automated monitoring tools if you manage multiple systems."
            };
        }
    }
}