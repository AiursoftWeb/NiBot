# Incremental image archiving

Use `archive` when a new download contains both old and new images and the old library
is already organized into folders. Existing images are read as references; they are
never moved, deleted, renamed or replaced. Incoming originals are retained.

The two stages serve different purposes:

* SHA-256 detects byte-identical files. Perceptual hash (pHash) finds possible duplicates
  despite encoding changes. A pHash score of 100 is **not** proof of identical images.
* CLIP image embeddings recommend a folder for images that passed the initial duplicate checks. Category centroid
  similarity, the margin over the second candidate and nearest-neighbor support must
  all pass their thresholds in strict mode. `Ready` means eligible for import under the
  selected policy, not proof that an image has never appeared in the archive.

Both roots are searched recursively. Each first-level destination folder is a category;
deeper images contribute to that first-level category. Unclassified images at the destination
root, and images under `Added` or `_review`, participate in deduplication only. Hidden
directories and symbolic links are skipped. Folder names such as `cluster_000_size_50`
are preserved, and folders may grow beyond their historical size.

## One-time model setup

NiBot is a .NET application. Inference runs locally on the CPU through ONNX Runtime;
Python, an Instagram login and a GPU are not required. Model production is a separate
workflow: obtain a compatible, self-contained ONNX vision model from a trusted publisher.
There is currently no bundled model or built-in public download URL.

Install a local model with the publisher's checksum:

```bash
nibot archive-model --source /path/to/clip.onnx --sha256 EXPECTED_SHA256
```

`--source` also accepts a final HTTPS download URL. Redirects are not followed. Installation
checks SHA-256 and the model contract before publishing the file, cleans up failed downloads,
and never overwrites different installed content. Reinstalling identical content is safe.
The default destination is the local application-data directory under
`NiBot/models/clip-vit-large-patch14.onnx`; `--destination` overrides it.
Alternatively use `archive --model /path/to/model.onnx` directly.

The model contract is a self-contained float32 CLIP vision graph with input `pixel_values`
of shape `[1,3,224,224]`, output `image_features` of shape `[1,D]`, and ONNX custom metadata
`nibot.archive=clip-vision-v1`. It must use the standard CLIP RGB normalization described
below. A filename alone cannot establish model compatibility. Model publishers should
validate the exported graph against their original encoder and publish its checksum and
license. The existing default model family is CLIP ViT-L/14; other compatible 224-pixel
vision encoders must use their own feature cache identity.

NiBot resizes the shortest side to 224 with bicubic sampling, center-crops, converts to
normalized channel-first RGB and normalizes the output vector. It uses ImageSharp for
preprocessing, so existing PIL/Transformers `.npz` vectors are **not directly imported**.
The old cache also lacks content checksums and a reliable preprocessing/version identity.
NiBot instead computes a new content-addressed cache for both reference and incoming images.
Cache keys include the model file SHA-256 and preprocessing version; changed images or
models cannot silently reuse incompatible entries. Existing `.npz` files are untouched.

## Generate a preview

```bash
nibot archive \
  --source /path/to/downloads/saved \
  --destination /path/to/Photos/Ins-Dancers-Clustered \
  --report /path/to/reports/import-001.json
```

For a source checkout, replace `nibot` with
`dotnet run --project src/Aiursoft.NiBot --`.

The command writes `import-001.json` and `import-001.json.html`. Open the HTML locally to
see source images, the top three folders and representative matching images. Local-image
rendering depends on the browser's file-access policy; the image links also open originals.
The report contains local paths and should be treated as private. A report path must be
new and outside both image roots. No images are copied during preview; only the report
and local feature cache are written.

Only `--source` and `--destination` are required after model setup. If `--report` is
omitted, a unique report is created under local application data / `NiBot/reports`, and
its path is printed. The feature cache defaults to local application data / `NiBot/archive-cache`.

| Decision | Meaning | Copied by `archive-apply`? |
| --- | --- | --- |
| `Duplicate` | Same SHA-256 as an existing/earlier incoming image, or an explicitly enabled pHash skip | No |
| `ReviewDuplicate` | Possible pHash duplicate; inspect both images | No |
| `Ready` | A category passed all configured classification checks | Yes |
| `Review` | Category unclear; top candidates are shown | No |
| `Error` | Source could not be read or embedded | No |

Uncertain items remain in the original download directory; there is no automatic move
into an arbitrary category or new `_review` folder. After reviewing a JSON item, you may
set its `Decision` to `Ready` and its `TargetFolder` to an existing category in the plan's
`Categories` list. Keep `Source` and `Sha256` unchanged. This also permits approving a
false-positive `ReviewDuplicate` as a new image. The executor validates paths and source
content before copying. Editing the plan is an explicit classification decision.

## Classification policy

`--classification strict` is the default. An ambiguous image remains `Review`.
`--classification nearest` selects the highest-ranked existing folder even if similarity,
margin or neighbor support is low. Use it when a reasonable closest category is sufficient:

```bash
nibot archive --source /path/to/downloads --destination /path/to/library \
  --classification nearest --inference-threads 16
```

Both modes retain duplicate checks and show the top three candidates in the report.
After CLIP ranking, an additional grayscale structure comparison checks the category
examples at full-frame and center-square framing. Very close structure is marked
`ReviewDuplicate` for inspection even with `--skip-similar`. This can catch some resized,
cropped and recolored versions missed by pHash, but may also flag similar compositions.
It is a review aid, not exhaustive duplicate detection or proof of identity. Arbitrary
crops, watermarks and edits can still be missed. Preview and inspect before importing.

## Apply the reviewed plan

```bash
nibot archive-apply --plan /path/to/reports/import-001.json
```

Alternatively, `archive --apply ...` creates the report and immediately copies its `Ready`
items. It does not copy unresolved review items. Use preview first when calibrating a library.

Copying uses a temporary file, validates its SHA-256 and then renames it into place without
overwriting. A same-name collision gets a deterministic SHA-256 suffix. A source that changed
since planning is rejected. Existing identical destination content is skipped even if it
was added after preview. Per-item results are written to `import-001.json.results.json`;
any failed items make the command return a nonzero exit code.

If a new or changed destination image is a possible perceptual duplicate since planning,
the executor leaves the incoming image untouched and records `ReviewDuplicate` (or
`SkippedSimilar` when the plan explicitly enabled that policy). Re-plan to review the
updated library. The destination hash snapshot is included in the JSON report.

The destination gets a small hidden `.nibot-archive` directory containing a lock file and
import records. Those records exclude imported images from future reference voting, so an
early classification mistake does not teach the classifier its own mistake. Keep these
records with the library. An interrupted copy may leave an ignored hidden `.tmp` file;
rerunning the plan is safe and will skip already published identical files. Do not run other
programs that edit the library concurrently; the lock coordinates NiBot apply operations only.

## Tuning

Defaults are starting points, **not calibrated probabilities**:

| Option | Default | Meaning |
| --- | --- | --- |
| `--classification` | `strict` | `nearest` chooses the first candidate without strict category thresholds |
| `--duplicate-similar` | `98` | pHash percentage for possible duplicates |
| `--skip-similar` | off | Automatically skip possible pHash duplicates instead of asking for review |
| `--min-similarity` | `0.75` | Minimum centroid cosine similarity |
| `--min-margin` | `0.03` | Minimum difference from second-best category |
| `--min-support` | `0.8` | Fraction of nearest reference images that must agree |
| `--neighbors` | `5` | Number of neighbors voting |
| `--exclude-category` | `Added`, `_review` | Reference exclusions; still checked for duplicates |
| `--inference-threads` | `2` | CPU inference threads |
| `--cache` | local application data / `NiBot/archive-cache` | Feature cache outside both image roots |

Repeat `--exclude-category` for multiple folders; providing values replaces the defaults.
Small categories may not have enough neighbors to pass support; they will still appear as
recommendations. Size-balanced clusters may overlap visually, so a low winning margin is
expected for some images. Test a representative sample and inspect errors before lowering
thresholds. Lowering the pHash threshold can confuse different poses in similar backgrounds.

For large libraries, work on a local snapshot of a network share to avoid interrupted
remote reads. Preserve the original folder structure and import journal. Before returning
new files, inspect a dry run, revalidate the live archive and verify copied contents.

CPU thread scaling depends on the machine. Benchmark a representative sample before
increasing `--inference-threads`; using every logical CPU is not necessarily faster.
Feature caching survives restarts, while the reference file index is rebuilt.

## Scope and validation

Supported image extensions: JPG, JPEG, PNG, JFIF, WebP and BMP, case-insensitively.
Video covers can be classified as images. MP4/Reels, captions and JSON metadata remain
in the download directory; this command does not deduplicate videos or infer video identity
from a matching cover. It does not rename or rebalance existing categories.

Tests cover duplicate handling, category ambiguity, exclusions, cache invalidation,
non-destructive previews, copying, retries, changed sources, path traversal, symlinks,
HTML escaping and the ONNX preprocessing/invocation path. The tiny ONNX fixture in the test
assets is a deterministic channel-mean graph, not a semantic model or an accuracy benchmark.
Model accuracy and export validation belong to the separate model publishing workflow.
