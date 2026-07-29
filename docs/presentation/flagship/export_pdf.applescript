-- Export SheikhGo flagship PPTX to PDF via Microsoft PowerPoint (macOS)
set pptxPath to POSIX path of ((path to me as text) & "::output:SheikhGo-AI-Fleet-Operations-Platform.pptx")
set pdfPath to POSIX path of ((path to me as text) & "::output:SheikhGo-AI-Fleet-Operations-Platform.pdf")

-- Prefer absolute paths relative to this script's folder
set scriptPath to POSIX path of (path to me)
set AppleScript's text item delimiters to "/"
set parts to text items of scriptPath
set AppleScript's text item delimiters to ""
if (count of parts) > 1 then
	set parentPath to ""
	repeat with i from 1 to ((count of parts) - 1)
		if i = 1 then
			set parentPath to item i of parts
		else
			set parentPath to parentPath & "/" & item i of parts
		end if
	end repeat
	set pptxPath to parentPath & "/output/SheikhGo-AI-Fleet-Operations-Platform.pptx"
	set pdfPath to parentPath & "/output/SheikhGo-AI-Fleet-Operations-Platform.pdf"
end if

tell application "Microsoft PowerPoint"
	activate
	open pptxPath
	delay 2
	set thePresentation to active presentation
	save thePresentation in pdfPath as save as PDF
	close thePresentation saving no
end tell
