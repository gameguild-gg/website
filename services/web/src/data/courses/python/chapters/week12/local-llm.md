# How to run Local LLMs

<details>
<summary>Lecture notes</summary>

- Running LLMs locally
- Installing OLLAMA locally
- Installing a model locally
- Using the model locally via CLI
- Using the model locally via Python

</details>

## Installing OLLAMA locally

Go to the [OLLAMA website](https://ollama.com/) and download the installer for your operating system. Follow the instructions to install OLLAMA.

## Installing a model locally

Search on their database models that fits into your available GPU memory size or RAM: [Ollama models](https://ollama.com/models). It is safe to assume gemma3 is the one that might fit small GPUs.

Open the terminal and run the following command to install a model:

``` bash
ollama pull gemma3
```

## Using the model locally via CLI

Once you have the model on your computer, you can use it via the command line interface (CLI). Open the terminal and run the following command:

``` bash
ollama run gemma3
```

Then you will be able to interact with the model via the command line. You can type your input and the model will generate a response.

## Using the model locally via Python

Once you have Ollama up and running on your machine, you can use it via Python via API. The full documentation is available [here](https://ollama.com/docs/api).

``` python
import requests

prompt = "Why the sky is blue?" # modify this with your prompt

data = {
  "model": "gemma3",
  "prompt": prompt,
  "stream": False
}

response = requests.post("http://localhost:11434/api/generate", json=data)
json_response = response.json()
message = json_response["response"]
print(message)
```

For the final project, I recommend you using replit(easier) or codespaces(better) and share de environment with your pairs. Pair up to 3 people. But you should work at least in pairs.
