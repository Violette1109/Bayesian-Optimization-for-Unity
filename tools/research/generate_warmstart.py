#!/usr/bin/env python3
import argparse
import json
import os
import re
from pathlib import Path

import requests


def parse_args():
    parser = argparse.ArgumentParser(description="Generate warm-start CSVs via Ollama.")
    parser.add_argument("--likert-max", type=int, default=5, help="Upper bound for mental_demand.")
    parser.add_argument("--rows", type=int, default=10, help="Number of generated rows.")
    parser.add_argument("--model", default="llama3.2:1b", help="Ollama model name.")
    parser.add_argument("--ollama-url", default="http://localhost:11434/api/generate", help="Ollama generation API URL.")
    parser.add_argument(
        "--output-dir",
        default=os.environ.get("BO_STUDY_DATA_ROOT", str(Path.cwd())) + "/InitData",
        help="Output directory for generated CSV files.",
    )
    parser.add_argument("--params-file", default="warmstart_params.csv", help="Filename for parameter CSV.")
    parser.add_argument("--objectives-file", default="warmstart_objectives.csv", help="Filename for objective CSV.")
    return parser.parse_args()


def build_prompt(likert_max: int, rows: int) -> str:
    return f"""You are an expert in human-computer interaction and motor control.
I need warm-start data for a Multi-Objective Bayesian Optimization study on circular movement tasks.

Parameters:
- circle_size: integer between 40 and 120 (pixel radius of the target circle)
- circle_distance: integer between 220 and 760 (pixel distance to target)
- movement_direction: integer between 0 and 180 (degrees)

Objectives:
- task_completion_time: integer between 0 and 120000 (milliseconds, smaller is better)
- accuracy: integer between 0 and 100 (percentage, larger is better)
- mental_demand: integer between 1 and {likert_max} (smaller is better)

Rules:
- Larger circles at shorter distances -> faster completion, higher accuracy, lower mental demand
- Smaller circles at longer distances -> slower completion, lower accuracy, higher mental demand
- movement_direction has moderate effect on all objectives
- Include diverse trade-off configurations spread across the full design space

Generate exactly {rows} rows.
Return ONLY JSON in this format:
{{
  "params": [{{"circle_size": 80, "circle_distance": 400, "movement_direction": 90}}],
  "objectives": [{{"task_completion_time": 5000, "accuracy": 85, "mental_demand": 2}}]
}}"""


def call_model(url: str, model: str, prompt: str) -> str:
    response = requests.post(
        url,
        json={
            "model": model,
            "prompt": prompt,
            "stream": False,
            "options": {"temperature": 0.7, "top_p": 0.95, "top_k": 20},
        },
        timeout=120,
    )
    response.raise_for_status()
    return response.json()["response"]


def extract_json(text: str) -> dict:
    cleaned = re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL)
    match = re.search(r"\{.*\}", cleaned, re.DOTALL)
    if not match:
        raise ValueError("No JSON object found in model response.")
    return json.loads(match.group())


def validate(data: dict, likert_max: int):
    params = data.get("params", [])
    objectives = data.get("objectives", [])
    if len(params) != len(objectives):
        raise ValueError(f"Row count mismatch: params={len(params)}, objectives={len(objectives)}")
    if len(params) < 2:
        raise ValueError("Need at least 2 rows.")

    for i, (p, o) in enumerate(zip(params, objectives)):
        assert 40 <= p["circle_size"] <= 120, f"Row {i}: circle_size out of bounds"
        assert 220 <= p["circle_distance"] <= 760, f"Row {i}: circle_distance out of bounds"
        assert 0 <= p["movement_direction"] <= 180, f"Row {i}: movement_direction out of bounds"
        assert 0 <= o["task_completion_time"] <= 120000, f"Row {i}: task_completion_time out of bounds"
        assert 0 <= o["accuracy"] <= 100, f"Row {i}: accuracy out of bounds"
        assert 1 <= o["mental_demand"] <= likert_max, f"Row {i}: mental_demand out of bounds"


def write_csvs(data: dict, output_dir: Path, params_file: str, objectives_file: str):
    output_dir.mkdir(parents=True, exist_ok=True)
    params_path = output_dir / params_file
    objectives_path = output_dir / objectives_file

    with params_path.open("w", encoding="utf-8", newline="") as f:
        f.write("circle_size;circle_distance;movement_direction\n")
        for p in data["params"]:
            f.write(f"{p['circle_size']};{p['circle_distance']};{p['movement_direction']}\n")

    with objectives_path.open("w", encoding="utf-8", newline="") as f:
        f.write("task_completion_time;accuracy;mental_demand\n")
        for o in data["objectives"]:
            f.write(f"{o['task_completion_time']};{o['accuracy']};{o['mental_demand']}\n")

    print(f"Written {len(data['params'])} rows:")
    print(f"  {params_path}")
    print(f"  {objectives_path}")


def main():
    args = parse_args()
    prompt = build_prompt(args.likert_max, args.rows)
    raw = call_model(args.ollama_url, args.model, prompt)
    data = extract_json(raw)
    validate(data, args.likert_max)
    write_csvs(data, Path(args.output_dir), args.params_file, args.objectives_file)


if __name__ == "__main__":
    main()
