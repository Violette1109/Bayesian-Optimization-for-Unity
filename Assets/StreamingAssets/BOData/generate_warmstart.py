import requests
import json
import os
import re
import sys

# ── 從命令行讀取 Likert 上限，默認 7 ──────────────────────
LIKERT_MAX = int(sys.argv[1]) if len(sys.argv) > 1 else 7

# ── 設定路徑 ──────────────────────────────────────────────
OUTPUT_DIR = os.path.expanduser(
    "~/Desktop/Bayesian-Optimization-for-Unity/Assets/StreamingAssets/BOData/InitData"
)
PARAMS_FILE = os.path.join(OUTPUT_DIR, "warmstart_params.csv")
OBJECTIVES_FILE = os.path.join(OUTPUT_DIR, "warmstart_objectives.csv")

# ── Ollama 設定 ───────────────────────────────────────────
OLLAMA_URL = "http://localhost:11434/api/generate"
MODEL = "qwen3.5:latest"
NUM_ROWS = 10

# ── Prompt ───────────────────────────────────────────────
PROMPT = f"""You are an expert in human-computer interaction and Fitts' Law.
I need warm-start data for a Multi-Objective Bayesian Optimization study on target clicking.

Parameters:
- TargetSize: continuous value between 0.3 and 0.4
- TargetEccentricity: continuous value between 0.0 and 1.0 (0=center, 1=far periphery)

Objectives (both smaller is better):
- PerceivedDifficultyLikert: integer between 1 and {LIKERT_MAX} (1=very easy, {LIKERT_MAX}=very hard)
- AverageClickTimeMS: integer between 50 and 3000

Domain rules:
- Larger targets at low eccentricity → low difficulty, fast clicks
- Smaller targets at high eccentricity → high difficulty, slow clicks
- Include diverse trade-off configurations spread across the full design space

Generate exactly {NUM_ROWS} rows of data.

Output ONLY a JSON object in this exact format, no explanation, no markdown:
{{
  "params": [
    {{"TargetSize": 0.40, "TargetEccentricity": 0.0}},
    ...
  ],
  "objectives": [
    {{"PerceivedDifficultyLikert": 1, "AverageClickTimeMS": 80}},
    ...
  ]
}}"""


def call_qwen(prompt):
    print(f"🤖 Calling Qwen3.5 via Ollama (Likert max = {LIKERT_MAX})...")
    response = requests.post(
        OLLAMA_URL,
        json={
            "model": MODEL,
            "prompt": prompt,
            "stream": False,
            "options": {
                "temperature": 0.7,
                "top_p": 0.95,
                "top_k": 20,
            },
        },
        timeout=120,
    )
    response.raise_for_status()
    return response.json()["response"]


def extract_json(text):
    text = re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL)
    match = re.search(r"\{.*\}", text, re.DOTALL)
    if not match:
        raise ValueError("No JSON found in response")
    return json.loads(match.group())


def validate_and_write(data):
    params = data["params"]
    objectives = data["objectives"]

    if len(params) != len(objectives):
        raise ValueError(f"Row count mismatch: params={len(params)}, objectives={len(objectives)}")
    if len(params) < 2:
        raise ValueError("Need at least 2 rows")

    for i, (p, o) in enumerate(zip(params, objectives)):
        assert 0.3 <= p["TargetSize"] <= 0.4, f"Row {i}: TargetSize out of bounds"
        assert 0.0 <= p["TargetEccentricity"] <= 1.0, f"Row {i}: TargetEccentricity out of bounds"
        assert 1 <= o["PerceivedDifficultyLikert"] <= LIKERT_MAX, f"Row {i}: Likert out of bounds (max={LIKERT_MAX})"
        assert 50 <= o["AverageClickTimeMS"] <= 3000, f"Row {i}: ClickTime out of bounds"

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    with open(PARAMS_FILE, "w") as f:
        f.write("TargetSize;TargetEccentricity\n")
        for p in params:
            f.write(f"{p['TargetSize']};{p['TargetEccentricity']}\n")

    with open(OBJECTIVES_FILE, "w") as f:
        f.write("PerceivedDifficultyLikert;AverageClickTimeMS\n")
        for o in objectives:
            f.write(f"{o['PerceivedDifficultyLikert']};{o['AverageClickTimeMS']}\n")

    print(f"✅ Written {len(params)} rows to:")
    print(f"   {PARAMS_FILE}")
    print(f"   {OBJECTIVES_FILE}")


def main():
    raw = call_qwen(PROMPT)
    print("📝 Raw response received, parsing...")
    data = extract_json(raw)
    validate_and_write(data)
    print(f"🎉 Done! Likert scale: 1-{LIKERT_MAX}")


if __name__ == "__main__":
    main()
