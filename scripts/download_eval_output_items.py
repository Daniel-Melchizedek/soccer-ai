import argparse
import json
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--endpoint", required=True)
    parser.add_argument("--eval-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    project_client = AIProjectClient(
        endpoint=args.endpoint,
        credential=DefaultAzureCredential(),
    )
    client = project_client.get_openai_client()
    output_items = list(
        client.evals.runs.output_items.list(
            run_id=args.run_id,
            eval_id=args.eval_id,
        )
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps([item.model_dump() for item in output_items], indent=2, default=str)
        + "\n",
        encoding="utf-8",
    )
    print(f"Saved {len(output_items)} output items to {args.output}")


if __name__ == "__main__":
    main()
import argparse
import json
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--endpoint", required=True)
    parser.add_argument("--eval-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    project_client = AIProjectClient(
        endpoint=args.endpoint,
        credential=DefaultAzureCredential(),
    )
    client = project_client.get_openai_client()
    output_items = list(
        client.evals.runs.output_items.list(
            run_id=args.run_id,
            eval_id=args.eval_id,
        )
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps([item.model_dump() for item in output_items], indent=2, default=str)
        + "\n",
        encoding="utf-8",
    )
    print(f"Saved {len(output_items)} output items to {args.output}")


if __name__ == "__main__":
    main()