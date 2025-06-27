====================================================
AgeisynthBot Cybersecurity Assistant - README FILE
====================================================
Project Details


Project Name: Ageisynth_CybersecurityBot_Part1
Type: WPF Application (.NET Framework, upgraded to .NET 8.0 for GUI)
Purpose: Interactive cybersecurity education and awareness tool designed to assist users in learning about online safety through conversational AI, task management, and quizzes.
Development Status: Active, with recent enhancements including a graphical user interface (GUI) and an Activity Log feature, merged from the part2poe branch into the main branch on GitHub as of June 27, 2025.
Creator: Developed as part of the Ageisynth Cybersecurity Awareness initiative, Sibongiseni Ngwamba.

User Guide
==================
Getting Started

Running the Application

Launch the Application: Execute the compiled .exe file from the project directory or run it via Visual Studio.
Audio Greeting: Upon startup, an audio greeting plays using the greeting.wav file.
File Location: The system searches for greeting.wav in the root project directory.
Error Handling: If the file is missing, an error message will be displayed, and the application will continue without audio.


ASCII Art Logo: An ASCII representation of the logo is generated from logo.jpg.
Conversion Process: The image is processed into text-based art for console compatibility (in early versions) and displayed in the GUI window.
Error Handling: If logo.jpg is missing, an error message will appear, but the application will proceed.


Welcome Banner: A welcome message appears, initiating the user setup process.

Initial Setup


First-Time Use: If it's your first session, enter your name when prompted. This is stored for future personalization.
Returning Users: If the application detects a saved name in memory.txt, it will ask, "Welcome back, [name]! Is that still you? (yes/no)". Confirming with "yes" or "no" either retains or updates the name.
Conversation Start: After identity confirmation, the chatbot greets you with personalized options, such as managing tasks, starting a quiz, or exploring cybersecurity topics.

Interacting with AgeisynthBot
==============================
Supported Cybersecurity Topics

You can engage the bot on a wide range of cybersecurity subjects, each with detailed responses:

Passwords: Learn about creating strong passwords (minimum 12 characters with mixed case, numbers, and symbols), using password managers (e.g., LastPass, Bitwarden), and enabling two-factor authentication (2FA).
Phishing: Understand how to identify phishing emails (e.g., suspicious sender addresses, urgent language), avoid clicking malicious links, and report incidents.
Privacy: Get advice on adjusting social media privacy settings, using VPNs, and protecting data from brokers.
Malware: Discover prevention tips (e.g., regular updates, antivirus software), detection signs (e.g., pop-ups, slowdowns), and recovery steps.
Firewalls: Explore configuration best practices (e.g., enabling default-deny policies) and the difference between hardware and software firewalls.
Scams: Learn to spot unsolicited offers, use secure payment methods, and stay updated on scam trends via resources like the FTC.
Attacks: Gain insights into common attack types (e.g., social engineering) and defense strategies (e.g., network segmentation).
SQL Injection: Master input validation, parameterized queries, and ORM usage to prevent SQL-based attacks.

Special Commands and Questions

AgeisynthBot responds to predefined phrases with tailored answers:

"how are you": Receives a cheerful response about the bot's readiness to assist.
"what is your purpose": Explains its role in cybersecurity education.
"what can i ask about": Lists supported topics and commands.
"who made you": Credits the Ageisynth initiative.
"help": Provides a detailed overview of available features.
"how do i exit": Instructs on using the "exit" command.

Example Interactions

You:-> Tell me about password security
AgeisynthBot:-> Make sure to use strong, unique passwords for each account. Aim for at least 12 characters with a mix of letters, numbers, and symbols. Consider using a password manager like LastPass or Bitwarden.

You:-> I'm interested in phishing
AgeisynthBot:-> Great! I'll remember that you're interested in phishing. It's an important aspect of cybersecurity!
Here's something about phishing: Be cautious of emails asking for personal information. Legitimate organizations rarely request sensitive details via email. Would you like to know how to identify suspicious emails?

Advanced Features
==================
Memory and Personalization


User Name Persistence: Stores your name in memory.txt across sessions.
Interest Tracking: Remembers topics you're interested in (e.g., "phishing") and offers related content when detected.
Memory Query: Use "what do you know about me" or "what do you remember" to view saved data, enhancing the personalized experience.

Sentiment Recognition


Emotional Detection: Analyzes input for emotions using a machine learning model (ML.NET).
Tailored Responses:
Worried: Offers reassurance (e.g., "I understand you might be worried... take small steps to improve security").
Confused: Simplifies explanations (e.g., "This can be confusing, let me explain simply...").
Frustrated: Provides patient guidance (e.g., "I can see this might be frustrating... let's take it step by step").
Curious: Encourages learning (e.g., "I'm glad you're curious! Learning about cybersecurity is key.").
Happy: Matches enthusiasm (e.g., "Great! I'm happy to share this with you!").


Feedback Loop: Users can correct sentiment predictions, improving the model's accuracy over time.

Follow-up Questions


Contextual Queries: After discussing a topic (e.g., "passwords"), the bot may ask follow-ups like "Would you like to know more about password managers?" or "Are you using two-factor authentication?".
Response Options: Answer "yes" or "no" to receive detailed or alternative advice, enhancing engagement.

Activity Log Feature (GUI)


Purpose: Tracks significant chatbot actions for user review.
Logged Actions:
Tasks: Adding (e.g., "Task added: 'Enable 2FA'"), updating, or marking as completed.
Reminders: Setting dates (e.g., "Reminder set for 'Review settings' on 2025-07-04").
Quiz Activity: Starting, answering questions, or completing (e.g., "Completed quiz with score 7/10").
NLP Interactions: Keyword-based responses or special command executions (e.g., "Answered special question 'help'").


Viewing the Log: Type "Show activity log" or "What have you done for me?" to see the last 5 actions in a numbered list within the GUI chat panel.
Example Output:Here’s a summary of recent actions:
1. Task added: 'Enable two-factor authentication' (Reminder set for 5 days from now).
2. Quiz started - 5 questions answered.
3. Reminder set: 'Review privacy settings' on 2025-07-04.


Implementation: Uses a List<string> (recentActions) to store logs, updated in real-time across all features.

Exiting the Application
===========================
To exit AgeisynthBot, simply type:
exit


Effect: Saves memory and tasks to their respective files, displays a farewell message, and closes the application.

Technical Notes

User Memory: Stored in memory.txt using a key-value format (e.g., name=John), loaded/saved on startup/exit.
Task Persistence: Saved in tasks.txt with fields separated by | (e.g., Title|Description|ReminderDate|Status), loaded on startup.
Keyword System: Matches user input against a predefined dictionary of topics and ignores common words (e.g., "the", "and") for relevance.
Sentiment Detection: Utilizes ML.NET with a custom-trained model, updated via user feedback.
Audio Support: Requires greeting.wav in the root directory; uses MediaPlayer for playback in the GUI version.
ASCII Logo: Generated from logo.jpg using an image-to-ASCII conversion algorithm (console version); displayed as a static image in the GUI.
File Requirements: Ensure greeting.wav and logo.jpg are in the project root (not bin\Debug).
GUI Enhancements: Transitioned from console to WPF, featuring a chat panel, task list, and quiz interface, developed in the part2poe branch.
GitHub Integration: The part2poe branch, containing GUI and Activity Log features, was merged into the main branch on June 27, 2025, at 10:53 AM SAST. Access the repository for the latest code and commit history.

Future Enhancements

Add a "Show more" option to view the full activity log.
Implement voice command support for hands-free interaction.
Expand quiz content with more advanced cybersecurity scenarios.


=======================================================
Thank you for using AgeisynthBot!Stay Cyber-Safe! 🛡️
=======================================================

Links to GitHub for Parts 1 and 2: https://github.com/sibongiseni-ngwamba/Ageisynth_CybersecurityBot_Part1.git
Links to GitHub for Part 3: 