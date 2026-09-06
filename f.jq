to_entries
| map(select(.key as $k
    | ($ids | split(",") | map(gsub("^\s+|\s+$";"")) | index($k)) != null))
| from_entries
