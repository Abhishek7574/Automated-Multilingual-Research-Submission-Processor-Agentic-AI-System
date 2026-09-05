# Automated Multilingual Research Submission Processor

An end-to-end **Agentic AI** system that automates the intake, validation, and review of research paper submissions — across languages, formats, and scale.

> Built as a capstone project demonstrating multi-agent orchestration, RAG-based Q&A, OCR, translation, and human-in-the-loop (HITL) review using the **Microsoft Agent Framework** and **Semantic Kernel**.

---

## 📖 Overview

Research submissions arrive in multiple formats (PDF, Word, scanned images) and languages, making manual review for formatting compliance, plagiarism, and quality both **slow and error-prone**.

This project automates that entire workflow with a team of specialized AI agents that:

- Ingest submissions (email + attachments)
- Detect language and translate content to English
- Extract structured metadata (title, authors, affiliations, abstract, keywords, figures)
- Validate formatting and compliance rules (page limits, required sections, references)
- Run plagiarism and toxicity/content checks
- Generate a concise, human-readable validation summary
- Support conversational, multilingual Q&A over submissions via RAG
- Route low-confidence or flagged items to a human reviewer, and **learn from corrections**

---

## ✨ Key Features

- 🌍 **Multilingual support** — automatic language detection and translation to English, with both original and translated versions retained
- 🖼️ **OCR support** for scanned/image-based submissions
- ✅ **Rule-based validation** — page count limits (8–25 pages), required sections (title, abstract, keywords, authors, references)
- 🕵️ **Plagiarism & toxicity checks** with automatic routing to human review
- 📝 **Auto-generated validation summaries** (≤250 words) highlighting key findings and issues
- 💬 **RAG-powered Q&A** — ask questions about stored submissions, with multilingual and chat-history support
- 🧑‍⚖️ **Human-in-the-Loop review** — admins can override/correct AI findings; the system flags items for review when deviation exceeds a 25% confidence threshold
- 📜 **Full audit trail** — every action taken by the system or an admin is logged

---

## 🤖 Agent Architecture

The system is built on a modular, multi-agent architecture where each agent owns a distinct responsibility in the pipeline:

| Agent | Responsibility |
|---|---|
| **Ingestion Agent** | Monitors for incoming submissions (reads from file system; live-mailbox monitoring is designed but not implemented) |
| **Pre-process Agent** | Validates file type, detects language, applies OCR to scanned documents |
| **Translation Agent** | Translates extracted content into English |
| **Extraction Agent** | Extracts structured fields — title, authors, affiliations, abstract, keywords, figures |
| **Validation Agent** | Applies business rules and semantic checks (page limits, required sections, plagiarism, toxicity) |
| **Summary Agent** | Produces a human-readable validation summary (max 250 words) |
| **RAG Agent** | Generates embeddings and maintains the vector store for retrieval |
| **Q&A Agent** | Handles conversational, multilingual queries with chat history support |
| **Human Feedback Agent** | Surfaces flagged/low-confidence items to admins, captures corrections, and feeds them back into the system |

Agents are orchestrated with **prompt templates** and leverage Semantic Kernel capabilities including:

- Semantic Functions
- Native Functions
- Memory
- Plugins
- Filters with Logging
- Agent Framework
- Process Framework

---

## 🏗️ Tech Stack

**Backend**
- C#
- Microsoft Agent Framework / Semantic Kernel
- Vector database (in-memory or other supported vector store)
- OCR engine (e.g., Tesseract) — optional
- Text generation & embedding models

**Frontend**
- Angular / React

**Deployment**
- Local

---

## 🚀 Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (for the C# backend)
- Node.js & npm (for the Angular/React frontend)
- An API key for your chosen LLM provider (e.g., Azure OpenAI / OpenAI)
- (Optional) Tesseract OCR installed locally for scanned-document support

### Installation

```bash
# Clone the repository
git clone https://github.com/Abhishek7574/Automated-Multilingual-Research-Submission-Processor-Agentic-AI-System.git
cd Automated-Multilingual-Research-Submission-Processor-Agentic-AI-System

# Restore backend dependencies
dotnet restore

# Install frontend dependencies
cd frontend
npm install
```

### Configuration

Add your model/API credentials (e.g., Azure OpenAI endpoint & key) to your local configuration/secrets file before running the backend. Do not commit secrets to source control.

### Running the app

```bash
# Run the backend
dotnet run --project <backend-project-path>

# In a separate terminal, run the frontend
cd frontend
npm start
```

---

## 📂 Sample Data

Sample research papers for testing can be downloaded from [arXiv](https://arxiv.org/).

---

## 📌 Non-Functional Considerations

- **Performance** — efficient processing of large submission batches
- **Scalability** — modular agent design supports horizontal scaling
- **Security** — secure handling of documents and (future) email access
- **Reliability** — robust error handling and fallback mechanisms
- **Usability** — intuitive UI for reviewers/admins
- **Maintainability** — modular, documented codebase

---

## 📦 Deliverables

- Solution design diagram
- Modularized C# source code
- Functional demo application (UI + backend agents)
- Sample multilingual research paper data
- Setup and usage documentation

---

## 📄 License

This project is available for educational and demonstration purposes. Add a license of your choice (e.g., MIT) if you plan to open-source it.

---

## 🙋 Author

**Abhishek Kumar**
