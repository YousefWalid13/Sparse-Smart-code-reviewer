# Models 
  - Code
	- severity (Critical
                High
                Medium
                Low
                Info)
  - Issue
  - Review
  - User


  # RelationShips 
   Code 1 - * Issue
   user	1 - * Review
   user 1 - * Code
   review 1 - * Issue


# repo flow 
    Repository URL
        ↓
  Download / Clone repository
        ↓
  Temporary Folder
        ↓
  Read files
        ↓
  Filter useful source files
        ↓
  Save Code records in DB
        ↓
  Delete temporary files


# both --> Git ,GitHub , ZIP
  use ProcessDirectoryAsync() for codereuse 
       Git                              
        ↓                               
       Clone                            
        ↓                               
       ProcessDirectoryAsync     
      - ------------------------
       GitHub
         ↓
        Download ZIP
         ↓
        Extract
         ↓
        ProcessDirectoryAsync